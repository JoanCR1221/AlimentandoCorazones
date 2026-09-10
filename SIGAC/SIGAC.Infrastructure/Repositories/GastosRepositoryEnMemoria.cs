using System.Collections.Concurrent;
using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación TEMPORAL en memoria, solo para desarrollo/pruebas: la
    // entidad GastoOperativo todavía no está configurada en SigacDbContext ni
    // tiene migración (tareas de Base de datos AB#2489/2491/2494, sin empezar),
    // así que todavía no se puede escribir un repositorio real con EF Core.
    public class GastosRepositoryEnMemoria : IGastosRepository
    {
        private readonly ConcurrentDictionary<int, GastoOperativo> _gastos = new();
        private int _siguienteId = 1;

        public Task AgregarAsync(GastoOperativo gasto)
        {
            gasto.Id = _siguienteId++;
            _gastos[gasto.Id] = gasto;
            return Task.CompletedTask;
        }

        public Task<GastoOperativo?> ObtenerPorIdAsync(int id)
        {
            _gastos.TryGetValue(id, out var gasto);
            return Task.FromResult(gasto);
        }

        public Task ActualizarAsync(GastoOperativo gasto)
        {
            _gastos[gasto.Id] = gasto;
            return Task.CompletedTask;
        }

        // Sin paginación a propósito: el listado necesita el conjunto filtrado
        // completo para poder sumar el total acumulado (AB#2549), no solo una
        // página. El volumen de gastos operativos de la asociación no justifica
        // paginar como sí hace falta en Existencias de inventario.
        public Task<IEnumerable<GastoOperativo>> ObtenerTodosAsync(FiltrosGastoDto filtros)
        {
            var query = _gastos.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(filtros.Categoria))
                query = query.Where(g => g.Categoria == filtros.Categoria);

            if (filtros.FechaDesde.HasValue)
                query = query.Where(g => g.Fecha >= filtros.FechaDesde.Value.Date);

            if (filtros.FechaHasta.HasValue)
                query = query.Where(g => g.Fecha <= filtros.FechaHasta.Value.Date);

            return Task.FromResult<IEnumerable<GastoOperativo>>(query.OrderByDescending(g => g.Fecha));
        }

        public Task AnularAsync(int id, string motivo)
        {
            if (_gastos.TryGetValue(id, out var gasto))
            {
                gasto.Estado = EstadoGastoOperativo.Anulado;
                gasto.MotivoAnulacion = motivo;
            }

            return Task.CompletedTask;
        }
    }
}
