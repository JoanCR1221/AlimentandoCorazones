using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Asistencia;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación real del repositorio con EF Core sobre SQL Server.
    // Respeta el contrato de IAsistenciaRepository sin cambiar su firma.
    // Reemplaza a AsistenciaRepositoryEnMemoria, que perdía todo al reiniciar.
    public class AsistenciaRepositoryEfCore : IAsistenciaRepository
    {
        private readonly SigacDbContext _context;

        public AsistenciaRepositoryEfCore(SigacDbContext context)
        {
            _context = context;
        }

        public async Task AgregarAsync(AsistenciaComedor asistencia)
        {
            _context.AsistenciasComedor.Add(asistencia);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida)
        {
            var dia = fecha.Date;

            // AnyAsync se traduce a un EXISTS: la base responde con un booleano y no
            // se materializa ninguna fila. Cubierto por UX_AsistenciasComedor_Beneficiario_Fecha_TiempoComida.
            return await _context.AsistenciasComedor
                .AsNoTracking()
                .AnyAsync(a =>
                    a.BeneficiarioId == beneficiarioId &&
                    a.Fecha == dia &&
                    a.TiempoComida == tiempoComida);
        }

        public async Task<IEnumerable<AsistenciaComedor>> ObtenerAsistenciasDiariasAsync(DateTime fecha)
        {
            // La fecha se normaliza acá y no dentro de la expresión: si el .Date
            // quedara del lado de la columna, EF traduciría CONVERT(date, [a].[Fecha])
            // y el índice de Fecha dejaría de poder usarse.
            var dia = fecha.Date;

            return await _context.AsistenciasComedor
                .AsNoTracking()
                .Where(a => a.Fecha == dia)
                .ToListAsync();
        }

        public async Task<IEnumerable<AsistenciaComedor>> ObtenerHistorialAsync(FiltrosAsistenciaDto filtros)
        {
            // Recién se ejecuta la consulta acá. El orden se fija en SQL para que el
            // resultado sea estable entre llamadas con los mismos filtros.
            return await ConstruirConsultaFiltrada(filtros)
                .OrderByDescending(a => a.Fecha)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
        }

        // Compone los filtros sobre un IQueryable: todo viaja a la base como WHERE.
        // Nada de LINQ to Objects, o habría que traer el historial entero para
        // filtrarlo en memoria, y crece con cada día registrado.
        private IQueryable<AsistenciaComedor> ConstruirConsultaFiltrada(FiltrosAsistenciaDto filtros)
        {
            // Include trae el beneficiario en el mismo viaje: sin él la navegación
            // llega en null y quien lea a.Beneficiario.NombreCompleto vería vacío.
            IQueryable<AsistenciaComedor> consulta = _context.AsistenciasComedor
                .AsNoTracking()
                .Include(a => a.Beneficiario);

            if (filtros.BeneficiarioId.HasValue)
            {
                var beneficiarioId = filtros.BeneficiarioId.Value;
                consulta = consulta.Where(a => a.BeneficiarioId == beneficiarioId);
            }

            if (!string.IsNullOrWhiteSpace(filtros.TiempoComida))
            {
                var tiempoComida = filtros.TiempoComida;
                consulta = consulta.Where(a => a.TiempoComida == tiempoComida);
            }

            if (filtros.FechaDesde.HasValue)
            {
                var desde = filtros.FechaDesde.Value.Date;
                consulta = consulta.Where(a => a.Fecha >= desde);
            }

            if (filtros.FechaHasta.HasValue)
            {
                var hasta = filtros.FechaHasta.Value.Date;
                consulta = consulta.Where(a => a.Fecha <= hasta);
            }

            return consulta;
        }
    }
}
