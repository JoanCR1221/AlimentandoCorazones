using SIGAC.Application.DTOs.Inventario;

namespace SIGAC.Application.Interfaces
{
    public interface IInventarioService
    {
        Task RegistrarEntradaAsync(EntradaInventarioCrearDto dto);
        Task RegistrarSalidaDonacionAsync(SalidaDonacionCrearDto dto);
        Task<IEnumerable<ArticuloExistenciaDto>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros);
        Task<ArticuloEditarDto?> ObtenerParaEditarAsync(int id);
        Task EditarArticuloAsync(int id, ArticuloEditarDto dto);
        Task<HistorialMovimientosResultadoDto> ObtenerHistorialMovimientosAsync(FiltrosMovimientoDto filtros);

        Task RegistrarSolicitudPrestamoAsync(SolicitudPrestamoCrearDto dto);
        Task AprobarPrestamoAsync(ResolucionPrestamoDto dto);
        Task RechazarPrestamoAsync(ResolucionPrestamoDto dto);
        Task<IEnumerable<SolicitudPrestamoListaDto>> ObtenerSolicitudesAsync();
    }
}