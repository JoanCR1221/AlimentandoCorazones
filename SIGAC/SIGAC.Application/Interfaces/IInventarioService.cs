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
        // Lo consume la alerta de stock bajo de la portada.
        Task<int> ContarArticulosStockBajoAsync();

        Task<ArticuloEditarDto?> ObtenerParaEditarAsync(int id);
        Task EditarArticuloAsync(int id, ArticuloEditarDto dto);
        Task EliminarArticuloAsync(int id);
        Task<HistorialMovimientosResultadoDto> ObtenerHistorialMovimientosAsync(FiltrosMovimientoDto filtros);

        // Lo mismo, pero de a una página (filtros.Pagina / TamanoPagina): para la
        // pantalla del historial. Trae de la base solo los movimientos de esa página,
        // no el historial entero; TotalRegistros, TotalEntradas y TotalSalidas siguen
        // cubriendo TODO el período filtrado. Los reportes usan el completo de arriba,
        // que ignora la paginación.
        Task<HistorialMovimientosResultadoDto> ObtenerPaginaHistorialMovimientosAsync(FiltrosMovimientoDto filtros);

        Task RegistrarSolicitudPrestamoAsync(SolicitudPrestamoCrearDto dto);
        Task AprobarPrestamoAsync(ResolucionPrestamoDto dto);
        Task RechazarPrestamoAsync(ResolucionPrestamoDto dto);
        // estado opcional: null devuelve todas, que es como lo llama hoy el frontend.
        Task<IEnumerable<SolicitudPrestamoListaDto>> ObtenerSolicitudesAsync(EstadoSolicitudPrestamo? estado = null);
    }
}