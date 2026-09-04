using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IInventarioService
    {
        Task RegistrarEntradaAsync(EntradaInventarioCrearDto dto);
        Task RegistrarSalidaDonacionAsync(SalidaDonacionCrearDto dto);
        Task<ResultadoPaginado<ArticuloExistenciaDto>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros);
        Task<ArticuloEditarDto?> ObtenerParaEditarAsync(int id);
        Task EditarArticuloAsync(int id, ArticuloEditarDto dto);
        Task EliminarArticuloAsync(int id);
        Task<HistorialMovimientosResultadoDto> ObtenerHistorialMovimientosAsync(FiltrosMovimientoDto filtros);

        Task RegistrarSolicitudPrestamoAsync(SolicitudPrestamoCrearDto dto);
        Task AprobarPrestamoAsync(ResolucionPrestamoDto dto);
        Task RechazarPrestamoAsync(ResolucionPrestamoDto dto);
        // estado opcional: null devuelve todas, que es como lo llama hoy el frontend.
        Task<IEnumerable<SolicitudPrestamoListaDto>> ObtenerSolicitudesAsync(EstadoSolicitudPrestamo? estado = null);
    }
}