using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Beneficiarios;
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
            await context.SaveChangesAsync();
        }

        public async Task ActualizarAsync(Beneficiario beneficiario)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // La entidad llega desprendida (ObtenerPorIdAsync usa AsNoTracking),
            // por lo que Update la adjunta y marca todos sus campos como modificados.
            context.Beneficiarios.Update(beneficiario);
            await context.SaveChangesAsync();
        }

        public async Task<Beneficiario?> ObtenerPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Beneficiarios
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<bool> ExisteAsync(string primerNombre, string segundoNombre, string primerApellido, string segundoApellido, DateTime fechaNacimiento, int? idExcluir = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var fecha = fechaNacimiento.Date;

            // La fecha de nacimiento se filtra en SQL (son pocos candidatos) y los
            // nombres se comparan en memoria: la normalización sin tildes no tiene
            // traducción a SQL y no hay collation que la garantice en la columna.
            var candidatos = await context.Beneficiarios
                .AsNoTracking()
                .Where(b => b.FechaNacimiento == fecha && (idExcluir == null || b.Id != idExcluir))
                .Select(b => new { b.PrimerNombre, b.SegundoNombre, b.PrimerApellido, b.SegundoApellido })
                .ToListAsync();

            return candidatos.Any(c =>
                TextoNormalizador.SonEquivalentes(c.PrimerNombre, primerNombre) &&
                TextoNormalizador.SonEquivalentes(c.SegundoNombre, segundoNombre) &&
                TextoNormalizador.SonEquivalentes(c.PrimerApellido, primerApellido) &&
                TextoNormalizador.SonEquivalentes(c.SegundoApellido, segundoApellido));
        }

        public async Task<bool> ExisteNumIdentidadAsync(string? numIdentidad, int? idExcluir = null)
        {
            // Los beneficiarios sin documento quedan fuera de la regla: son varias
            // personas indocumentadas y no pueden chocar entre sí. Es la misma
            // exclusión que hace el filtro del índice único.
            if (string.IsNullOrEmpty(numIdentidad))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            // Solo el número: el tipo de documento no participa. Un mismo número
            // cargado como cédula y como "Otro" es la misma persona escrita dos
            // veces, y así lo entiende también el índice único de la base.
            // Igualdad directa, la misma comparación que hace el índice único
            // filtrado, para que el código y la base coincidan en qué es duplicado
            // y la consulta pueda hacer seek sobre ese índice.
            // El "sin distinguir mayúsculas" lo aporta la collation CI de SQL Server.
            return await context.Beneficiarios
                .AsNoTracking()
                .AnyAsync(b =>
                    b.NumIdentidad == numIdentidad &&
                    (idExcluir == null || b.Id != idExcluir));
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
        private static IQueryable<Beneficiario> AplicarOrdenYPaginado(
            IQueryable<Beneficiario> consulta, FiltrosBeneficiarioDto filtros)
        {
            var tamanoPagina = filtros.TamanoPaginaEfectivo;

            return consulta
                .OrderBy(b => b.PrimerApellido)
                .ThenBy(b => b.SegundoApellido)
                .ThenBy(b => b.PrimerNombre)
                .ThenBy(b => b.SegundoNombre)
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
                var categoria = filtros.Categoria;
                consulta = consulta.Where(b => b.Categoria == categoria);
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
    }
}
