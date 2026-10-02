using SIGAC.Application.DTOs.Alquileres;

namespace SIGAC.Application.Interfaces
{
    public interface IAlquileresService
    {
        // Devuelve el Id del alquiler recién creado.
        Task<int> RegistrarAlquilerAsync(AlquilerCrearDto dto);

        Task<HistorialAlquileresResultadoDto> ObtenerHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros);

        // Cancelar y no eliminar: el alquiler queda en el historial con su motivo y
        // deja de ocupar el horario.
        Task CancelarAlquilerAsync(CancelacionAlquilerDto dto);
    }
}
