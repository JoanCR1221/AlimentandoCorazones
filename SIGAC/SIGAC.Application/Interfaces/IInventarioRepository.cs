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
        Task AgregarArticuloAsync(Articulo articulo);
        Task ActualizarArticuloAsync(Articulo articulo);
        Task ActualizarStockAsync(int articuloId, int cantidad);
        Task ReducirStockAsync(int articuloId, int cantidad);

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