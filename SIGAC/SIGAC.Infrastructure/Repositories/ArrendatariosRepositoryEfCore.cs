using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación EF Core de los arrendatarios. Mismo patrón que el resto: cada
    // método abre su propio DbContext desde el factory (ver Program.cs).
    public class ArrendatariosRepositoryEfCore : IArrendatariosRepository
    {
        // Collation acentuada-insensible para el buscador, igual que en Donantes.
        private const string ColacionSinTildes = "Latin1_General_CI_AI";

        // Violación de unicidad en SQL Server, mismo criterio que
        // InventarioRepositoryEfCore.
        private const int ErrorSqlRestriccionUnica = 2627;
        private const int ErrorSqlIndiceUnico = 2601;

        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public ArrendatariosRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AgregarAsync(Arrendatario arrendatario)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Arrendatarios.Add(arrendatario);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
                                               (sql.Number == ErrorSqlRestriccionUnica || sql.Number == ErrorSqlIndiceUnico))
            {
                // El único índice único de la tabla es UX_Arrendatarios_Identificacion:
                // otra alta con la misma identificación se metió entre el chequeo del
                // servicio y este INSERT.
                throw new DuplicateException(
                    $"Ya existe un arrendatario con la identificación {arrendatario.Identificacion}.");
            }
        }

        public async Task<Arrendatario?> ObtenerPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Arrendatarios
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Arrendatario?> BuscarPorIdentificacionAsync(string identificacion)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Igualdad directa, sin collation: es la misma comparación que hace
            // UX_Arrendatarios_Identificacion y puede hacer seek sobre ese índice.
            return await context.Arrendatarios
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Identificacion == identificacion);
        }

        public async Task<IReadOnlyList<Arrendatario>> BuscarActivosAsync(string texto, int maximo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Por nombre (parcial, sin tildes) o por identificación (prefijo, sin
            // guiones ni espacios: se guarda así). "rojas" encuentra "Ana Rojas" y
            // "1234" encuentra la cédula 123456789.
            var identificacion = texto.Replace("-", string.Empty).Replace(" ", string.Empty);

            return await context.Arrendatarios
                .AsNoTracking()
                .Where(a => a.Estado)
                .Where(a => EF.Functions.Collate(a.Nombre, ColacionSinTildes).Contains(texto)
                            || (identificacion.Length > 0 && a.Identificacion != null && a.Identificacion.StartsWith(identificacion)))
                .OrderBy(a => a.Nombre)
                .ThenBy(a => a.Id)
                .Take(maximo)
                .ToListAsync();
        }
    }
}
