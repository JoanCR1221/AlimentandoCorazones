using SIGAC.Application.DTOs.Asistencia;

namespace SIGAC.Application.Interfaces
{
    public interface IAsistenciaService
    {
        Task RegistrarAsistenciaAsync(AsistenciaCrearDto dto);
        Task<HistorialAsistenciaResultadoDto> ObtenerHistorialAsistenciaAsync(FiltrosAsistenciaDto filtros);

        // Consulta de existencia, no de datos: la pantalla de registro la llama cada
        // vez que cambia el beneficiario, la fecha o el tiempo de comida. Se traduce
        // a un EXISTS, en vez de traer el historial entero para contar filas.
        Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida);
    }
}