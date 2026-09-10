using System.Collections.Concurrent;
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
    }
}
