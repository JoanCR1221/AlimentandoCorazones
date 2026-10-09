using SIGAC.Application.DTOs.Alquileres;

namespace SIGAC.Application.Interfaces
{
    public interface IAlquileresService
    {
        // Devuelve el Id del alquiler recién creado.
        Task<int> RegistrarAlquilerAsync(AlquilerCrearDto dto);

        Task<HistorialAlquileresResultadoDto> ObtenerHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros);

        // Lo mismo, pero de a una página (filtros.Pagina / TamanoPagina): para el
        // calendario. Trae de la base solo los alquileres de esa página, no todos los
        // que cumplen el filtro; TotalRegistros y TotalesPorMoneda siguen cubriendo
        // TODO el conjunto filtrado. El reporte y la ocupación del día usan el de
        // arriba, que ignora la paginación.
        Task<HistorialAlquileresResultadoDto> ObtenerPaginaHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros);

        // Cancelar y no eliminar: el alquiler queda en el historial con su motivo y
        // deja de ocupar el horario.
        Task CancelarAlquilerAsync(CancelacionAlquilerDto dto);
    }
}
