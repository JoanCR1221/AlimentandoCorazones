using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    public class TiposGastoRepositoryEfCore : ITiposGastoRepository
    {
        // Números de error de SQL Server para violación de unicidad: 2627 es una
        // restricción UNIQUE/PK y 2601 un índice único. Mismo criterio que
        // BeneficiariosRepositoryEfCore; acá el único índice es UX_TiposGasto_Nombre.
        private const int ErrorSqlRestriccionUnica = 2627;
        private const int ErrorSqlIndiceUnico = 2601;

        // Factory y no un DbContext inyectado, por lo mismo que en los demás
        // repositorios del proyecto: en Blazor Server el scope dura toda la sesión
        // y DbContext no tolera dos operaciones a la vez.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public TiposGastoRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IReadOnlyList<TipoGasto>> ObtenerTodosAsync(bool soloActivos)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var query = context.TiposGasto.AsNoTracking();

            if (soloActivos)
                query = query.Where(t => t.Activo);

            return await query.OrderBy(t => t.Nombre).ToListAsync();
        }

        public async Task<TipoGasto?> ObtenerPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.TiposGasto
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task AgregarAsync(TipoGasto tipo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.TiposGasto.Add(tipo);
            await GuardarTraduciendoDuplicadoAsync(context, tipo.Nombre);
        }

        public async Task ActualizarAsync(TipoGasto tipo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existente = await context.TiposGasto.FirstOrDefaultAsync(t => t.Id == tipo.Id);
            if (existente is null)
                return;

            // Campo por campo y no Update(), por el mismo motivo que en
            // GastosRepositoryEfCore.ActualizarAsync: Activo se mueve solo por
            // CambiarEstadoAsync y no puede pisarse con lo que traiga la entidad.
            existente.Nombre = tipo.Nombre;
            existente.GeneraInventario = tipo.GeneraInventario;
            existente.CuentaContablePorDefecto = tipo.CuentaContablePorDefecto;

            await GuardarTraduciendoDuplicadoAsync(context, tipo.Nombre);
        }

        public async Task CambiarEstadoAsync(int id, bool activo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            await context.TiposGasto
                .Where(t => t.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, activo));
        }

        // Dos altas simultáneas del mismo nombre pasan las dos el chequeo previo
        // del servicio y la segunda la frena UX_TiposGasto_Nombre. Sin traducir, el
        // usuario veía "Intente de nuevo".
        private static async Task GuardarTraduciendoDuplicadoAsync(SigacDbContext context, string nombre)
        {
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
                (sql.Number == ErrorSqlRestriccionUnica || sql.Number == ErrorSqlIndiceUnico))
            {
                throw new DuplicateException($"Ya existe el tipo de gasto '{nombre}'.");
            }
        }
    }
}
