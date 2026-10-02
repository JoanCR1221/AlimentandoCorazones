using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IGastosRepository
    {
        Task AgregarAsync(GastoOperativo gasto);
        Task<GastoOperativo?> ObtenerPorIdAsync(int id);
        Task ActualizarAsync(GastoOperativo gasto);

        // Trae cada gasto con su TipoGasto cargado: el listado muestra el nombre.
        Task<IEnumerable<GastoOperativo>> ObtenerTodosAsync(FiltrosGastoDto filtros);

        // Proveedores distintos que contienen el texto, ordenados, a lo sumo maximo.
        Task<IReadOnlyList<string>> BuscarProveedoresAsync(string texto, int maximo);

        /// <summary>
        /// Anula el gasto y, en la misma operación, todas las entradas de inventario
        /// que lo respaldaban, revirtiendo el stock de cada una.
        /// </summary>
        // Una sola operación y no dos, porque anular el gasto sin revertir sus
        // entradas deja el inventario afirmando que hay existencias respaldadas por
        // un gasto que oficialmente no ocurrió.
        Task AnularConEntradasVinculadasAsync(int gastoId, string motivo);

        // Cifras agregadas para el panorama gráfico de Gastos (ver
        // ReportesService.ObtenerPanoramaGastosAsync). Todas sobre una ventana de
        // mesesHaciaAtras meses y solo gastos activos y en colones, salvo
        // ObtenerConteoPorEstadoAsync, que cuenta los dos estados a propósito.
        Task<IReadOnlyList<MontoPorTipoDto>> ObtenerMontoPorTipoAsync(int mesesHaciaAtras);
        Task<IReadOnlyList<MontoPorFormaPagoDto>> ObtenerMontoPorFormaPagoAsync(int mesesHaciaAtras);
        Task<IReadOnlyList<MontoPorMesDto>> ObtenerMontoPorMesAsync(int mesesHaciaAtras);
        Task<IReadOnlyList<ConteoPorMesDto>> ObtenerCantidadPorMesAsync(int mesesHaciaAtras);
        Task<IReadOnlyList<MontoPorProveedorDto>> ObtenerTopProveedoresAsync(int mesesHaciaAtras, int maximo);
        Task<(int Activos, int Anulados)> ObtenerConteoPorEstadoAsync(int mesesHaciaAtras);
    }
}
