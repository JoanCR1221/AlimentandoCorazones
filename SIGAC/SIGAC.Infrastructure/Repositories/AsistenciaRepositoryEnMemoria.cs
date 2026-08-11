using System.Collections.Concurrent;
using SIGAC.Application.DTOs.Asistencia;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación TEMPORAL en memoria, solo para desarrollo/pruebas.
    public class AsistenciaRepositoryEnMemoria : IAsistenciaRepository
    {
        private readonly ConcurrentDictionary<int, AsistenciaComedor> _asistencias = new();
        private int _siguienteId = 1;

        public Task AgregarAsync(AsistenciaComedor asistencia)
        {
            asistencia.Id = _siguienteId++;
            _asistencias[asistencia.Id] = asistencia;
            return Task.CompletedTask;
        }

        public Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida)
        {
            var existe = _asistencias.Values.Any(a =>
                a.BeneficiarioId == beneficiarioId &&
                a.Fecha.Date == fecha.Date &&
                a.TiempoComida == tiempoComida);
            return Task.FromResult(existe);
        }

        public Task<IEnumerable<AsistenciaComedor>> ObtenerAsistenciasDiariasAsync(DateTime fecha)
        {
            var resultado = _asistencias.Values.Where(a => a.Fecha.Date == fecha.Date);
            return Task.FromResult(resultado);
        }

        public Task<IEnumerable<AsistenciaComedor>> ObtenerHistorialAsync(FiltrosAsistenciaDto filtros)
        {
            var query = _asistencias.Values.AsEnumerable();

            if (filtros.BeneficiarioId.HasValue)
                query = query.Where(a => a.BeneficiarioId == filtros.BeneficiarioId.Value);

            if (!string.IsNullOrWhiteSpace(filtros.TiempoComida))
                query = query.Where(a => a.TiempoComida == filtros.TiempoComida);

            if (filtros.FechaDesde.HasValue)
                query = query.Where(a => a.Fecha.Date >= filtros.FechaDesde.Value.Date);

            if (filtros.FechaHasta.HasValue)
                query = query.Where(a => a.Fecha.Date <= filtros.FechaHasta.Value.Date);

            return Task.FromResult(query);
        }
    }
}