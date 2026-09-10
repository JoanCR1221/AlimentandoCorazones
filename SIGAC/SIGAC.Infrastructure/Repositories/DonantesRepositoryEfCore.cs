using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación real del repositorio con EF Core sobre SQL Server.
    // Respeta el contrato de IDonantesRepository sin cambiar su firma.
    public class DonantesRepositoryEfCore : IDonantesRepository
    {
        // Collation acentuada-insensible (AI) para la búsqueda de texto: hace que
        // "Jose" encuentre a "José" sin salir de SQL. Se aplica a la expresión,
        // no a la columna, así que no depende de la collation de la base.
        private const string ColacionSinTildes = "Latin1_General_CI_AI";

        // Factory y no un DbContext inyectado: en Blazor Server el scope dura toda
        // la sesión, así que un contexto compartido queda expuesto a que dos
        // operaciones lo usen a la vez (por ejemplo, la grilla de donantes
        // recargando mientras se registra una donación), y DbContext no tolera eso.
        // Cada método pide su propio contexto de corta vida.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public DonantesRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AgregarAsync(Donante donante)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Donantes.Add(donante);

            // Sin try/catch de violación de unicidad, a diferencia de
            // InventarioRepositoryEfCore: no hay ningún índice único sobre Donantes
            // que pueda rechazar este INSERT. IX_Donantes_Nombre es un índice común
            // a propósito, porque dos donantes homónimos son dos personas distintas.
            await context.SaveChangesAsync();
        }

        public async Task<Donante?> ObtenerPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Donantes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task ActualizarAsync(Donante donante)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existente = await context.Donantes
                .FirstOrDefaultAsync(d => d.Id == donante.Id);

            if (existente is null)
                return;

            // Campo por campo y no Update(), por el mismo motivo que en
            // ActualizarArticuloAsync: Update() marcaría TODAS las columnas como
            // modificadas y reescribiría también Estado y FechaRegistro con lo que
            // trajera la entidad desprendida. Estado se mueve por otro camino
            // (CambiarEstadoAsync), así que una desactivación hecha entre la lectura
            // y el guardado quedaría pisada; FechaRegistro es un dato histórico que
            // no se corrige desde la edición.
            existente.Nombre = donante.Nombre;
            existente.TipoPersona = donante.TipoPersona;
            existente.Telefono = donante.Telefono;
            existente.Correo = donante.Correo;

            await context.SaveChangesAsync();
        }

        public async Task<ResultadoPaginado<Donante>> ObtenerTodosAsync(FiltrosDonanteDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Un solo IQueryable con los filtros aplicados. Todavía no se ejecutó
            // nada contra la base: se materializa recién en el Count y en el ToList.
            var consulta = ConstruirConsultaFiltrada(context, filtros);

            // Consulta 1: cuántos donantes cumplen los filtros, para que la grilla
            // sepa cuántas páginas hay.
            var total = await consulta.CountAsync();

            // Consulta 2: solo la página pedida. ToListAsync obligatorio: el contexto
            // se libera al salir del método y un IQueryable diferido explotaría al
            // recorrerlo desde la página.
            var tamanoPagina = filtros.TamanoPaginaEfectivo;

            var elementos = await consulta
                // El OrderBy es obligatorio para que Skip/Take sea determinista. El
                // desempate por Id evita que dos homónimos se intercambien de página
                // entre una consulta y la siguiente.
                .OrderBy(d => d.Nombre)
                .ThenBy(d => d.Id)
                .Skip(filtros.PaginaEfectiva * tamanoPagina)
                .Take(tamanoPagina)
                .ToListAsync();

            return new ResultadoPaginado<Donante>(elementos, total);
        }

        // Compone los filtros sobre un IQueryable: todo se traduce a SQL y se aplica
        // ANTES de paginar. Nada de LINQ to Objects acá, o la paginación no serviría
        // de nada (habría que traer la tabla entera para filtrarla en memoria).
        private static IQueryable<Donante> ConstruirConsultaFiltrada(SigacDbContext context, FiltrosDonanteDto filtros)
        {
            var consulta = context.Donantes.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filtros.Nombre))
            {
                // Búsqueda parcial y acento-insensible, igual que la caja del listado
                // de existencias: quien escribe "jose" espera encontrar "José
                // Rodríguez". Es lo contrario de ExisteNombreAsync, que compara en
                // exacto; acá se busca, allá se identifica.
                var busqueda = filtros.Nombre.Trim();
                consulta = consulta.Where(d =>
                    EF.Functions.Collate(d.Nombre, ColacionSinTildes).Contains(busqueda));
            }

            if (!string.IsNullOrWhiteSpace(filtros.TipoPersona))
            {
                // Igualdad exacta: el tipo de persona se elige de una lista cerrada
                // (TiposPersonaDonante), no se teclea.
                var tipoPersona = filtros.TipoPersona;
                consulta = consulta.Where(d => d.TipoPersona == tipoPersona);
            }

            // HasValue y no un bool directo: el filtro tiene tres estados. Con null
            // no se agrega ninguna condición y salen activos e inactivos por igual.
            // Cubierto por IX_Donantes_Estado.
            if (filtros.Estado.HasValue)
            {
                var estado = filtros.Estado.Value;
                consulta = consulta.Where(d => d.Estado == estado);
            }

            return consulta;
        }

        public async Task<bool> ExisteNombreAsync(string nombre, int? idExcluir = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Comparación EXACTA (sin Contains y sin collation AI), a diferencia del
            // filtro del listado: este método responde "¿ya hay alguien registrado
            // con este mismo nombre?" para que el servicio pueda AVISAR de un posible
            // duplicado. Con búsqueda parcial, registrar a "Ana" avisaría por
            // "Ana María" y "Mariana", que son personas distintas: el aviso se
            // volvería ruido y se terminaría ignorando.
            //
            // Se compara sobre el nombre recortado porque es como lo guarda el
            // servicio. El "sin distinguir mayúsculas" lo aporta la collation CI de
            // SQL Server, igual que en el resto del proyecto.
            //
            // Un true NO impide guardar: IX_Donantes_Nombre no es único a propósito.
            var nombreBuscado = nombre.Trim();

            return await context.Donantes
                .AsNoTracking()
                .AnyAsync(d => d.Nombre == nombreBuscado && (idExcluir == null || d.Id != idExcluir));
        }

        public async Task CambiarEstadoAsync(int id, bool estado)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var donante = await context.Donantes
                .FirstOrDefaultAsync(d => d.Id == id);

            // Sin entidad no hay nada que cambiar: se sale en silencio, igual que
            // CambiarEstadoAsync de Beneficiarios. Quien necesite distinguir "no
            // existe" de "ya estaba así" consulta antes con ObtenerPorIdAsync.
            if (donante is not null)
            {
                donante.Estado = estado;
                await context.SaveChangesAsync();
            }
        }
    }
}
