using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IInventarioRepository
    {
        // Artículos
        Task<Articulo?> ObtenerArticuloPorNombreAsync(string nombre);
        Task<Articulo?> ObtenerArticuloPorIdAsync(int id);
        Task ActualizarArticuloAsync(Articulo articulo);
        // Devuelve una sola página, ya filtrada y ordenada en SQL, junto con el
        // total de registros que cumplen los filtros (que la grilla necesita para
        // saber cuántas páginas hay). Nunca materializa el catálogo entero.
        Task<ResultadoPaginado<Articulo>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros);

        // idExcluir permite editar un artículo sin chocar consigo mismo. El nombre
        // es la clave natural del catálogo (ver UX_Articulos_Nombre); el código es
        // opcional y solo choca cuando ambos lo tienen definido (ver UX_Articulos_Codigo).
        Task<bool> ExisteNombreAsync(string nombre, int? idExcluir = null);
        Task<bool> ExisteCodigoAsync(string? codigo, int? idExcluir = null);

        // Un artículo con historial (entradas, salidas o solicitudes de préstamo)
        // no se puede borrar: el movimiento es el respaldo contable y no puede
        // quedar huérfano. EliminarArticuloAsync no hace este chequeo por su cuenta
        // (además de que el Restrict de las FK lo respalda en la base); quien llama
        // decide si procede después de consultar este método.
        Task<bool> TieneMovimientosAsync(int articuloId);
        Task EliminarArticuloAsync(int articuloId);

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

        // estado opcional: null devuelve todas. El filtro se resuelve en SQL sobre
        // IX_SolicitudesPrestamo_Estado, que existe justamente para la bandeja de
        // solicitudes pendientes.
        Task<IEnumerable<SolicitudPrestamo>> ObtenerSolicitudesAsync(EstadoSolicitudPrestamo? estado = null);
    }
}
