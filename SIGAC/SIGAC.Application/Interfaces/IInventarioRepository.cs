using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IInventarioRepository
    {
        // Artículos
        Task<Articulo?> ObtenerArticuloPorNombreAsync(string nombre);
        Task<Articulo?> ObtenerArticuloPorIdAsync(int id);
        Task AgregarArticuloAsync(Articulo articulo);
        Task ActualizarArticuloAsync(Articulo articulo);
        Task ActualizarStockAsync(int articuloId, int cantidad);
        Task ReducirStockAsync(int articuloId, int cantidad);
        Task<IEnumerable<Articulo>> ObtenerExistenciasAsync(string? nombre, string? categoria);

        // Entradas y salidas
        Task AgregarEntradaAsync(EntradaInventario entrada);
        Task AgregarSalidaAsync(SalidaInventario salida);
        Task<IEnumerable<EntradaInventario>> ObtenerEntradasAsync(int? articuloId, DateTime? desde, DateTime? hasta);
        Task<IEnumerable<SalidaInventario>> ObtenerSalidasAsync(int? articuloId, DateTime? desde, DateTime? hasta);

        // Préstamos
        Task AgregarSolicitudPrestamoAsync(SolicitudPrestamo solicitud);
        Task<SolicitudPrestamo?> ObtenerSolicitudPorIdAsync(int id);
        Task ActualizarSolicitudAsync(SolicitudPrestamo solicitud);
        Task<IEnumerable<SolicitudPrestamo>> ObtenerSolicitudesAsync();
    }
}