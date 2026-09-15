using Microsoft.EntityFrameworkCore;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    public class PermisosRepositoryEfCore : IPermisosRepository
    {
        // Factory y no DbContext inyectado, por lo mismo que en el resto de los
        // repositorios (ver README de Infrastructure).
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public PermisosRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IReadOnlyList<string>> ObtenerRevocadosAsync(string usuarioId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.PermisosRevocados
                .AsNoTracking()
                .Where(p => p.UsuarioId == usuarioId)
                .OrderBy(p => p.Permiso)
                .Select(p => p.Permiso)
                .ToListAsync();
        }

        public async Task ReemplazarRevocadosAsync(string usuarioId, IEnumerable<string> permisos)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaccion = await context.Database.BeginTransactionAsync();

            // Borrar y volver a insertar dentro de una transacción: si la inserción
            // falla, el usuario no queda sin revocaciones a medias.
            await context.PermisosRevocados
                .Where(p => p.UsuarioId == usuarioId)
                .ExecuteDeleteAsync();

            var ahora = DateTime.Now;

            // Distinct: el índice único UX_PermisosRevocados_Usuario_Permiso
            // rechazaría un duplicado, y el panel no debería mandarlos, pero
            // tolerarlo acá evita un error confuso por un doble clic.
            var nuevas = permisos
                .Distinct(StringComparer.Ordinal)
                .Select(p => new PermisoRevocadoUsuario
                {
                    UsuarioId = usuarioId,
                    Permiso = p,
                    FechaRegistro = ahora
                });

            context.PermisosRevocados.AddRange(nuevas);
            await context.SaveChangesAsync();

            await transaccion.CommitAsync();
        }
    }
}
