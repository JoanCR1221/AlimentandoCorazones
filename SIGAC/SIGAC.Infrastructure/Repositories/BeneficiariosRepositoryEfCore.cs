using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación real del repositorio con EF Core sobre SQL Server.
    // Respeta el contrato de IBeneficiariosRepository sin cambiar su firma.
    public class BeneficiariosRepositoryEfCore : IBeneficiariosRepository
    {
        // Collation acentuada-insensible (AI) para la búsqueda de texto: hace que
        // "Maria" encuentre a "María" sin salir de SQL. Se aplica a la expresión,
        // no a la columna, así que no depende de la collation de la base.
        private const string ColacionSinTildes = "Latin1_General_CI_AI";

        // Números de error de SQL Server para violación de unicidad: 2627 es una
        // restricción UNIQUE/PK y 2601 un índice único. Mismo criterio que
        // InventarioRepositoryEfCore.
        private const int ErrorSqlRestriccionUnica = 2627;
        private const int ErrorSqlIndiceUnico = 2601;

        // SQL Server nombra el índice violado en el texto del error, en cualquier
        // idioma: así se sabe cuál de los dos índices únicos rechazó la escritura.
        private const string IndiceUnicoNumIdentidad = "UX_Beneficiarios_NumIdentidad";

        // Factory y no un DbContext inyectado: en Blazor Server el scope dura toda
        // la sesión, así que un contexto compartido queda expuesto a que dos
        // operaciones lo usen a la vez (por ejemplo, la grilla recargando mientras
        // se cambia el estado de una fila), y DbContext no tolera eso. Cada método
        // pide su propio contexto de corta vida.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public BeneficiariosRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AgregarAsync(Beneficiario beneficiario)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Beneficiarios.Add(beneficiario);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                // Dos altas simultáneas del mismo beneficiario pasan las dos el
                // chequeo previo del servicio y la segunda la frena el índice
                // único. Sin traducir, el usuario veía "Intente de nuevo".
                throw new DuplicateException(DescribirDuplicado(ex, esOtro: false));
            }
        }

        public async Task ActualizarAsync(Beneficiario beneficiario)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // La entidad llega desprendida (ObtenerPorIdAsync usa AsNoTracking),
            // por lo que Update la adjunta y marca todos sus campos como modificados.
            context.Beneficiarios.Update(beneficiario);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                throw new DuplicateException(DescribirDuplicado(ex, esOtro: true));
            }
        }

        private static bool EsViolacionDeUnicidad(DbUpdateException ex) =>
            ex.InnerException is SqlException sql &&
            (sql.Number == ErrorSqlRestriccionUnica || sql.Number == ErrorSqlIndiceUnico);

        // Mismos textos que los chequeos previos del servicio, según CUÁL índice
        // rechazó la escritura. Si no se lo puede identificar, se cae al de
        // nombres, que es la clave que siempre participa.
        private static string DescribirDuplicado(DbUpdateException ex, bool esOtro)
        {
            var sujeto = esOtro ? "otro beneficiario" : "un beneficiario";

            if (ex.InnerException is SqlException sql &&
                sql.Message.Contains(IndiceUnicoNumIdentidad, StringComparison.OrdinalIgnoreCase))
            {
                return $"Ya existe {sujeto} registrado con ese número de identidad.";
            }

            return $"Ya existe {sujeto} con esos nombres, apellidos y fecha de nacimiento.";
        }

        public async Task<Beneficiario?> ObtenerPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Beneficiarios
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<BeneficiarioCoincidente?> BuscarPorNombresYFechaAsync(string primerNombre, string segundoNombre, string primerApellido, string segundoApellido, DateTime fechaNacimiento, int? idExcluir = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var fecha = fechaNacimiento.Date;

            // La fecha de nacimiento se filtra en SQL (son pocos candidatos) y los
            // nombres se comparan en memoria: la normalización sin tildes no tiene
            // traducción a SQL y no hay collation que la garantice en la columna.
            var candidatos = await context.Beneficiarios
                .AsNoTracking()
                .Where(b => b.FechaNacimiento == fecha && (idExcluir == null || b.Id != idExcluir))
                .Select(b => new { b.Id, b.PrimerNombre, b.SegundoNombre, b.PrimerApellido, b.SegundoApellido, b.Estado })
                .ToListAsync();

            var coincidente = candidatos.FirstOrDefault(c =>
                TextoNormalizador.SonEquivalentes(c.PrimerNombre, primerNombre) &&
                TextoNormalizador.SonEquivalentes(c.SegundoNombre, segundoNombre) &&
                TextoNormalizador.SonEquivalentes(c.PrimerApellido, primerApellido) &&
                TextoNormalizador.SonEquivalentes(c.SegundoApellido, segundoApellido));

            return coincidente is null
                ? null
                : new BeneficiarioCoincidente(
                    coincidente.Id,
                    ReglasBeneficiario.ComponerNombreCompleto(
                        coincidente.PrimerNombre, coincidente.SegundoNombre, coincidente.PrimerApellido, coincidente.SegundoApellido),
                    coincidente.Estado);
        }

        public async Task<BeneficiarioCoincidente?> BuscarPorNumIdentidadAsync(string? numIdentidad, int? idExcluir = null)
        {
            // Los beneficiarios sin documento quedan fuera de la regla: son varias
            // personas indocumentadas y no pueden chocar entre sí. Es la misma
            // exclusión que hace el filtro del índice único.
            if (string.IsNullOrEmpty(numIdentidad))
                return null;

            await using var context = await _contextFactory.CreateDbContextAsync();

            // Solo el número: el tipo de documento no participa. Un mismo número
            // cargado como cédula y como "Otro" es la misma persona escrita dos
            // veces, y así lo entiende también el índice único de la base.
            // Igualdad directa, la misma comparación que hace el índice único
            // filtrado, para que el código y la base coincidan en qué es duplicado
            // y la consulta pueda hacer seek sobre ese índice.
            // El "sin distinguir mayúsculas" lo aporta la collation CI de SQL Server.
            var coincidente = await context.Beneficiarios
                .AsNoTracking()
                .Where(b =>
                    b.NumIdentidad == numIdentidad &&
                    (idExcluir == null || b.Id != idExcluir))
                .Select(b => new { b.Id, b.PrimerNombre, b.SegundoNombre, b.PrimerApellido, b.SegundoApellido, b.Estado })
                .FirstOrDefaultAsync();

            return coincidente is null
                ? null
                : new BeneficiarioCoincidente(
                    coincidente.Id,
                    ReglasBeneficiario.ComponerNombreCompleto(
                        coincidente.PrimerNombre, coincidente.SegundoNombre, coincidente.PrimerApellido, coincidente.SegundoApellido),
                    coincidente.Estado);
        }

        public async Task<ResultadoPaginado<Beneficiario>> ObtenerPaginaAsync(FiltrosBeneficiarioDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Un solo IQueryable con los filtros aplicados. Todavía no se ejecutó
            // nada contra la base: se materializa recién en el Count y en el ToList.
            var consulta = ConstruirConsultaFiltrada(context, filtros);

            // Consulta 1: cuántos registros cumplen los filtros. Lo necesita el
            // paginador para saber cuántas páginas hay.
            var total = await consulta.CountAsync();

            // Consulta 2: solo la página pedida.
            var elementos = await AplicarOrdenYPaginado(consulta, filtros).ToListAsync();

            return new ResultadoPaginado<Beneficiario>(elementos, total);
        }

        // El OrderBy es obligatorio para que Skip/Take sea determinista. Se traduce
        // a ORDER BY ... OFFSET n ROWS FETCH NEXT m ROWS ONLY: la base devuelve solo
        // las filas de la página, no se descarta nada en memoria.
        //
        // Alfabético en el mismo orden en que se lee NombreCompleto (nombres y
        // después apellidos), que es como lo muestran el listado y los buscadores
        // de beneficiario. Ordenar por apellido mientras se muestra el nombre
        // primero hacía que la lista no pareciera ordenada. Coincide además con
        // las primeras columnas del índice único, así que SQL puede recorrerlo.
        private static IQueryable<Beneficiario> AplicarOrdenYPaginado(
            IQueryable<Beneficiario> consulta, FiltrosBeneficiarioDto filtros)
        {
            var tamanoPagina = filtros.TamanoPaginaEfectivo;

            return consulta
                .OrderBy(b => b.PrimerNombre)
                .ThenBy(b => b.SegundoNombre)
                .ThenBy(b => b.PrimerApellido)
                .ThenBy(b => b.SegundoApellido)
                .ThenBy(b => b.Id)
                .Skip(filtros.PaginaEfectiva * tamanoPagina)
                .Take(tamanoPagina);
        }

        // Compone los filtros sobre un IQueryable: todo se traduce a SQL y se aplica
        // ANTES de paginar. Nada de LINQ to Objects acá, o la paginación no serviría
        // de nada (habría que traer la tabla entera para filtrarla en memoria).
        private static IQueryable<Beneficiario> ConstruirConsultaFiltrada(SigacDbContext context, FiltrosBeneficiarioDto filtros)
        {
            var consulta = context.Beneficiarios.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filtros.Nombre))
            {
                var busqueda = filtros.Nombre.Trim();

                // Una sola caja para los cuatro campos de nombre y el número de
                // identidad: sirve tanto "María" como "123456789".
                // El número no lleva collation: son dígitos y letras sin tildes.
                consulta = consulta.Where(b =>
                    EF.Functions.Collate(b.PrimerNombre, ColacionSinTildes).Contains(busqueda) ||
                    EF.Functions.Collate(b.SegundoNombre, ColacionSinTildes).Contains(busqueda) ||
                    EF.Functions.Collate(b.PrimerApellido, ColacionSinTildes).Contains(busqueda) ||
                    EF.Functions.Collate(b.SegundoApellido, ColacionSinTildes).Contains(busqueda) ||
                    (b.NumIdentidad != null && b.NumIdentidad.Contains(busqueda)));
            }

            if (!string.IsNullOrWhiteSpace(filtros.Categoria))
            {
                // La categoría no se guarda: se traduce al rango de fechas de
                // nacimiento que le corresponde hoy (ver CategoriasBeneficiario).
                // Una categoría que no existe no devuelve nada, igual que antes.
                if (!CategoriasBeneficiario.EsValida(filtros.Categoria))
                    return consulta.Where(_ => false);

                var (nacidoDespuesDe, nacidoHasta) = CategoriasBeneficiario.RangoDeNacimiento(filtros.Categoria);

                if (nacidoDespuesDe is DateTime despuesDe)
                    consulta = consulta.Where(b => b.FechaNacimiento > despuesDe);

                if (nacidoHasta is DateTime hasta)
                    consulta = consulta.Where(b => b.FechaNacimiento <= hasta);
            }

            if (!string.IsNullOrWhiteSpace(filtros.TipoDocumento))
            {
                var tipoDocumento = filtros.TipoDocumento;
                consulta = consulta.Where(b => b.TipoDocumento == tipoDocumento);
            }

            if (filtros.Estado.HasValue)
            {
                var estado = filtros.Estado.Value;
                consulta = consulta.Where(b => b.Estado == estado);
            }

            return consulta;
        }

        public async Task CambiarEstadoAsync(int id, bool estado)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var beneficiario = await context.Beneficiarios
                .FirstOrDefaultAsync(b => b.Id == id);

            if (beneficiario is not null)
            {
                beneficiario.Estado = estado;
                await context.SaveChangesAsync();
            }
        }

        public async Task<ResumenRegistrosDto> ObtenerResumenAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Meses calendario, con la misma hora local con la que se guarda
            // FechaRegistro (DateTime.Now en el servicio).
            var inicioMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var inicioMesAnterior = inicioMes.AddMonths(-1);

            // Un solo GROUP BY constante: SQL Server devuelve una fila con los
            // cuatro conteos y no se trae ningún beneficiario. Con la tabla vacía
            // no hay grupo, de ahí el Vacio.
            var resumen = await context.Beneficiarios
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new ResumenRegistrosDto(
                    g.Count(b => b.Estado),
                    g.Count(b => !b.Estado),
                    g.Count(b => b.FechaRegistro >= inicioMes),
                    g.Count(b => b.FechaRegistro >= inicioMesAnterior && b.FechaRegistro < inicioMes)))
                .FirstOrDefaultAsync();

            return resumen ?? ResumenRegistrosDto.Vacio;
        }

        public async Task<IReadOnlyList<ConteoPorCategoriaDto>> ObtenerConteoPorCategoriaAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Solo activos: el panorama por categoría es "a quién atendemos hoy",
            // no el histórico completo del padrón (eso ya lo separa Activos/Inactivos).
            var activos = context.Beneficiarios.AsNoTracking().Where(b => b.Estado);

            var resultado = new List<ConteoPorCategoriaDto>();

            // Una consulta por categoría, mismo criterio que el filtro de categoría
            // en ObtenerPaginaAsync: la categoría no se guarda, se traduce al rango
            // de fecha de nacimiento que le corresponde hoy (CategoriasBeneficiario
            // es la única fuente de esos rangos).
            foreach (var categoria in CategoriasBeneficiario.Todas)
            {
                var (nacidoDespuesDe, nacidoHasta) = CategoriasBeneficiario.RangoDeNacimiento(categoria);

                var consulta = activos;

                if (nacidoDespuesDe is DateTime despuesDe)
                    consulta = consulta.Where(b => b.FechaNacimiento > despuesDe);

                if (nacidoHasta is DateTime hasta)
                    consulta = consulta.Where(b => b.FechaNacimiento <= hasta);

                resultado.Add(new ConteoPorCategoriaDto(categoria, await consulta.CountAsync()));
            }

            return resultado;
        }

        public async Task<IReadOnlyList<ConteoPorMesDto>> ObtenerAltasPorMesAsync(int mesesHaciaAtras)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var inicioVentana = InicioVentana(mesesHaciaAtras);

            // Se proyecta a un tipo anónimo y no directamente al record: EF Core no
            // traduce un GroupBy + Select a un constructor posicional seguido de
            // OrderBy, y falla en tiempo de ejecución (no en compilación).
            var filas = await context.Beneficiarios
                .AsNoTracking()
                .Where(b => b.FechaRegistro >= inicioVentana)
                .GroupBy(b => new { b.FechaRegistro.Year, b.FechaRegistro.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Cantidad = g.Count() })
                .OrderBy(f => f.Year).ThenBy(f => f.Month)
                .ToListAsync();

            return filas.Select(f => new ConteoPorMesDto(f.Year, f.Month, f.Cantidad)).ToList();
        }

        // Primer día del mes que queda mesesHaciaAtras meses atrás, contando el mes
        // actual como el primero: con mesesHaciaAtras = 12 la ventana cubre este mes
        // y los 11 anteriores.
        private static DateTime InicioVentana(int mesesHaciaAtras)
        {
            var inicioMesActual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return inicioMesActual.AddMonths(-(mesesHaciaAtras - 1));
        }
    }
}
