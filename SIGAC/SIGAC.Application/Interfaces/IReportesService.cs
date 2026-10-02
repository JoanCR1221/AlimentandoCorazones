using SIGAC.Application.DTOs.Reportes;

namespace SIGAC.Application.Interfaces
{
    // Reportes institucionales consolidados (módulo de Generación de reportes).
    // Un método por reporte: Donaciones, Inventario y Gastos se agregan acá
    // mismo cuando les toque, sin una interfaz nueva por cada uno.
    public interface IReportesService
    {
        Task<ReporteBeneficiariosResultadoDto> GenerarReporteBeneficiariosAsync(FiltrosReporteBeneficiariosDto filtros);

        // Panorama gráfico de Beneficiarios: cifras agregadas para las gráficas de
        // /reportes/beneficiarios/panorama, sobre una ventana fija de meses hacia
        // atrás (no depende de filtros de pantalla, a diferencia del reporte de arriba).
        Task<PanoramaBeneficiariosDto> ObtenerPanoramaBeneficiariosAsync();

        // Panorama gráfico de Gastos Operativos: mismo criterio que el de
        // Beneficiarios, sobre una ventana fija de meses hacia atrás. Sugerido por
        // el cliente; no tiene un reporte exportable detrás todavía.
        Task<PanoramaGastosDto> ObtenerPanoramaGastosAsync();

        // Reporte contable de gastos: mismo formato que usa hoy la contadora
        // (agrupado por tipo de gasto y descripción de cuenta, con subtotales y
        // gran total), para un mes y una forma de pago.
        Task<ReporteGastosDto> GenerarReporteGastosAsync(FiltrosReporteGastosDto filtros);
    }
}
