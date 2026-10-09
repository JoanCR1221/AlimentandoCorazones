using SIGAC.Application.DTOs;
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
        // Sin paginar: lo usa el selector de gastos de Inventario, que necesita el
        // conjunto completo. El listado de gastos usa ObtenerPaginaAsync.
        Task<IEnumerable<GastoOperativo>> ObtenerTodosAsync(FiltrosGastoDto filtros);

        // Una página del listado (filtros.Pagina / TamanoPagina), del más reciente al
        // más antiguo, con su TipoGasto cargado, más cuántos gastos cumplen el filtro
        // en total. Mismos filtros que ObtenerTodosAsync, resueltos en SQL.
        Task<ResultadoPaginado<GastoOperativo>> ObtenerPaginaAsync(FiltrosGastoDto filtros);

        // Total pagado (monto sin IVA + IVA) de los gastos ACTIVOS que cumplen el
        // filtro, por moneda: un gasto anulado ya no es dinero gastado. Cubre TODO el
        // conjunto filtrado e ignora la paginación.
        Task<IReadOnlyList<MontoPorMonedaDto>> ObtenerTotalesPorMonedaAsync(FiltrosGastoDto filtros);

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

        // Para el reporte contable de gastos: un mes calendario y una forma de
        // pago, igual que el papel de la contadora. Solo activos: uno anulado no
        // es un gasto. Con TipoGasto incluido porque el reporte agrupa por su
        // nombre; la agrupación y los subtotales los arma ReportesService, mismo
        // criterio que ObtenerParaReporteBeneficiariosAsync.
        Task<IEnumerable<GastoOperativo>> ObtenerParaReporteAsync(int mes, int anio, string formaPago);
    }
}
