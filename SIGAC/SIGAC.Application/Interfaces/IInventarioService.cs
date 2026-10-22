using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    // Una entrada ya validada y con su artículo resuelto, lista para escribirse pero
    // todavía sin guardar. Articulo es el destino: uno existente (Id > 0) o uno nuevo
    // que la escritura debe crear (Id = 0, y Entrada.Articulo apunta a él).
    // CreaArticulo es true solo en la primera entrada de un artículo nuevo.
    public sealed record EntradaInventarioPreparada(EntradaInventario Entrada, Articulo Articulo, bool CreaArticulo);

    public interface IInventarioService
    {
        Task RegistrarEntradaAsync(EntradaInventarioCrearDto dto);
        Task RegistrarSalidaDonacionAsync(SalidaDonacionCrearDto dto);

        // Validan y resuelven pero NO guardan: para que Donaciones escriba el
        // movimiento de stock dentro de la misma transacción que su propio registro.
        Task<IReadOnlyList<EntradaInventarioPreparada>> PrepararEntradasAsync(IReadOnlyList<EntradaInventarioCrearDto> entradas);
        Task<SalidaInventario> PrepararSalidaDonacionAsync(SalidaDonacionCrearDto dto);

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
    }
}