using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación real del repositorio con EF Core sobre SQL Server, que
    // reemplaza a GastosRepositoryEnMemoria ahora que GastoOperativo está
    // configurada en SigacDbContext y tiene migración.
    // Respeta el contrato de IGastosRepository sin cambiar su firma.
    public class GastosRepositoryEfCore : IGastosRepository
    {
        // Factory y no un DbContext inyectado, por lo mismo que en los demás
        // repositorios del proyecto: en Blazor Server el scope dura toda la sesión
        // y un contexto compartido queda expuesto a que dos operaciones lo usen a
        // la vez (por ejemplo, el listado de gastos recargando mientras se anula
        // uno), y DbContext no tolera eso. Cada método pide su propio contexto.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public GastosRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AgregarAsync(GastoOperativo gasto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.GastosOperativos.Add(gasto);
            await context.SaveChangesAsync();
        }

        public async Task<GastoOperativo?> ObtenerPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Sin AsNoTracking: el servicio usa lo que devuelve este método como
            // entidad de trabajo para editar y después llama a ActualizarAsync, que
            // abre su propio contexto. El tracking acá no sirve para eso (contextos
            // distintos), pero tampoco estorba, y mantenerlo evita sorpresas si el
            // servicio empieza a apoyarse en la identidad de la instancia.
            return await context.GastosOperativos
                .FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task ActualizarAsync(GastoOperativo gasto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // La entidad viene de otro contexto (el de ObtenerPorIdAsync), así que
            // este no la está siguiendo: hay que adjuntarla y marcarla modificada
            // para que EF Core genere el UPDATE.
            context.GastosOperativos.Update(gasto);
            await context.SaveChangesAsync();
        }

        // Sin paginación a propósito: el listado necesita el conjunto filtrado
        // completo para poder sumar el total acumulado (AB#2549), no solo una
        // página. El volumen de gastos operativos de la asociación no justifica
        // paginar como sí hace falta en Existencias de inventario.
        public async Task<IEnumerable<GastoOperativo>> ObtenerTodosAsync(FiltrosGastoDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // AsNoTracking: es una consulta de solo lectura que alimenta la grilla,
            // no se edita nada de lo que devuelve.
            var query = context.GastosOperativos.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtros.Categoria))
                query = query.Where(g => g.Categoria == filtros.Categoria);

            if (filtros.FechaDesde.HasValue)
            {
                var desde = filtros.FechaDesde.Value.Date;
                query = query.Where(g => g.Fecha >= desde);
            }

            if (filtros.FechaHasta.HasValue)
            {
                // Intervalo semiabierto [desde, hasta+1día) en vez de <= hasta.Date.
                // Fecha es datetime2, así que un gasto fechado a media mañana del
                // último día del rango es > hasta.Date y quedaría fuera del filtro.
                // Hoy el date picker manda siempre medianoche y las dos formas dan
                // lo mismo, pero esta no depende de eso.
                var hastaExclusivo = filtros.FechaHasta.Value.Date.AddDays(1);
                query = query.Where(g => g.Fecha < hastaExclusivo);
            }

            return await query
                .OrderByDescending(g => g.Fecha)
                .ToListAsync();
        }

        public async Task AnularAsync(int id, string motivo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var gasto = await context.GastosOperativos
                .FirstOrDefaultAsync(g => g.Id == id);

            // Silencioso si no existe, igual que la versión en memoria: quien decide
            // si un id inexistente es error es GastosService, que ya lo valida antes
            // de llegar acá (lanza NotFoundException).
            if (gasto is null)
                return;

            gasto.Estado = EstadoGastoOperativo.Anulado;
            gasto.MotivoAnulacion = motivo;

            await context.SaveChangesAsync();
        }
    }
}
