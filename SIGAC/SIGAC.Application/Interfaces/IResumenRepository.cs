using SIGAC.Application.DTOs;

namespace SIGAC.Application.Interfaces
{
    // Consultas agregadas de solo lectura para las tarjetas de resumen de cada
    // módulo. Todas devuelven conteos y sumas calculados en SQL: ninguna trae
    // filas. Beneficiarios y Donantes tienen su propio ObtenerResumenAsync en sus
    // repositorios porque ahí ya vivía todo lo de esas tablas.
    public interface IResumenRepository
    {
        Task<ResumenDonacionesDto> ObtenerResumenDonacionesAsync();
        Task<ResumenInventarioDto> ObtenerResumenInventarioAsync();
        Task<ResumenAsistenciaDto> ObtenerResumenAsistenciaAsync();
        Task<ResumenGastosDto> ObtenerResumenGastosAsync();
        Task<ResumenProyectosDto> ObtenerResumenProyectosAsync();
    }
}
