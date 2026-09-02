using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IInventarioRepository
    {
        // Artículos
        Task<Articulo?> ObtenerArticuloPorNombreAsync(string nombre);
        Task<Articulo?> ObtenerArticuloPorIdAsync(int id);
        Task ActualizarArticuloAsync(Articulo articulo);
        Task<IEnumerable<Articulo>> ObtenerExistenciasAsync(string? nombre, string? categoria);

        // Movimientos que mueven stock
        //
        // Son operaciones COMPUESTAS a propósito: cada una agrupa todas las
        // escrituras de un movimiento (el artículo nuevo si hace falta, la fila del
        // movimiento y el ajuste de StockActual) para que la implementación pueda
        // resolverlas todo-o-nada. Antes estaban partidas en métodos sueltos
        // (AgregarEntradaAsync + ActualizarStockAsync, etc.) y cada uno confirmaba
        // por su cuenta: si el segundo fallaba, el primero ya estaba guardado y el
        // stock quedaba desfasado del historial de movimientos.
        //
        // Por eso ya no existe ningún método que inserte un movimiento sin tocar el
        // stock, ni que toque el stock sin registrar el movimiento: separarlos es
        // exactamente lo que producía la inconsistencia.

        /// <summary>
        /// Registra la entrada y suma su cantidad al stock del artículo.
        /// Si <paramref name="articuloNuevo"/> no es null, lo crea en la misma
        /// operación y le asigna el Id generado a la entrada.
        /// </summary>
        Task RegistrarEntradaConStockAsync(EntradaInventario entrada, Articulo? articuloNuevo);

        /// <summary>
        /// Registra la salida y descuenta su cantidad del stock del artículo.
        /// </summary>
        Task RegistrarSalidaConStockAsync(SalidaInventario salida);

        /// <summary>
        /// Marca la solicitud con el estado y motivo que traiga, registra la salida
        /// asociada y descuenta el stock, todo en la misma operación.
        /// </summary>
        Task AprobarPrestamoConStockAsync(SolicitudPrestamo solicitud, SalidaInventario salida);

        // Consultas de movimientos
        Task<IEnumerable<EntradaInventario>> ObtenerEntradasAsync(int? articuloId, DateTime? desde, DateTime? hasta);
        Task<IEnumerable<SalidaInventario>> ObtenerSalidasAsync(int? articuloId, DateTime? desde, DateTime? hasta);

        // Préstamos
        Task AgregarSolicitudPrestamoAsync(SolicitudPrestamo solicitud);
        Task<SolicitudPrestamo?> ObtenerSolicitudPorIdAsync(int id);
        Task ActualizarSolicitudAsync(SolicitudPrestamo solicitud);
        Task<IEnumerable<SolicitudPrestamo>> ObtenerSolicitudesAsync();
    }
}
