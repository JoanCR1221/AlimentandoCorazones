using SIGAC.Application.DTOs.Asistencia;

namespace SIGAC.Application.Interfaces
{
    public interface IAsistenciaService
    {
        Task RegistrarAsistenciaAsync(AsistenciaCrearDto dto);
        Task<HistorialAsistenciaResultadoDto> ObtenerHistorialAsistenciaAsync(FiltrosAsistenciaDto filtros);
    }
}