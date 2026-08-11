using SIGAC.Domain.Entities;
using SIGAC.Application.DTOs.Asistencia;

namespace SIGAC.Application.Interfaces
{
    public interface IAsistenciaRepository
    {
        Task AgregarAsync(AsistenciaComedor asistencia);
        Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida);
        Task<IEnumerable<AsistenciaComedor>> ObtenerAsistenciasDiariasAsync(DateTime fecha);
        Task<IEnumerable<AsistenciaComedor>> ObtenerHistorialAsync(FiltrosAsistenciaDto filtros);
    }
}