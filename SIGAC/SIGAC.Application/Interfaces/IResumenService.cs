using SIGAC.Application.DTOs;

namespace SIGAC.Application.Interfaces
{
    // Cifras de las tarjetas de resumen de los módulos (ver IResumenRepository).
    public interface IResumenService
    {
        Task<ResumenDonacionesDto> ObtenerResumenDonacionesAsync();
        Task<ResumenInventarioDto> ObtenerResumenInventarioAsync();
        Task<ResumenAsistenciaDto> ObtenerResumenAsistenciaAsync();
        Task<ResumenGastosDto> ObtenerResumenGastosAsync();
        Task<ResumenProyectosDto> ObtenerResumenProyectosAsync();
    }
}
