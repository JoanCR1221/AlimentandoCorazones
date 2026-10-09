using SIGAC.Application.DTOs.Reportes;

namespace SIGAC.Application.Interfaces
{
    // Reportes institucionales consolidados (módulo de Generación de reportes).
    // Un método por reporte: Inventario, Proyectos y Alquileres se agregan acá
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

        // Reporte de donaciones recibidas (dinero y especie) de un período, con el
        // total de dinero por moneda.
        Task<ReporteDonacionesResultadoDto> GenerarReporteDonacionesAsync(FiltrosReporteDonacionesDto filtros);

        // Panorama gráfico de Donaciones: mismo criterio que los de Beneficiarios y
        // Gastos, sobre una ventana fija de meses hacia atrás.
        Task<PanoramaDonacionesDto> ObtenerPanoramaDonacionesAsync();

        // Reporte de alquileres de espacios de un período: reservados y cancelados,
        // horas alquiladas e ingreso por moneda (los cancelados no suman).
        Task<ReporteAlquileresResultadoDto> GenerarReporteAlquileresAsync(FiltrosReporteAlquileresDto filtros);

        // Reporte de movimientos de inventario (entradas, donaciones y préstamos) de un
        // período, con las unidades que entraron y las que salieron.
        Task<ReporteMovimientosResultadoDto> GenerarReporteMovimientosAsync(FiltrosReporteMovimientosDto filtros);

        // Reporte de proyectos comunitarios de un período (por fecha de inicio), con el
        // total de proyectos por estado y de participantes.
        Task<ReporteProyectosResultadoDto> GenerarReporteProyectosAsync(FiltrosReporteProyectosDto filtros);

        // Panorama gráfico de Proyectos: totales por estado y por tipo de participante
        // de todos los proyectos, y dos series mensuales sobre una ventana fija.
        Task<PanoramaProyectosDto> ObtenerPanoramaProyectosAsync();

        // Panorama gráfico de Inventario: mismo criterio que los otros, sobre una
        // ventana fija de meses hacia atrás. Todo en unidades, sin las entradas
        // anuladas.
        Task<PanoramaInventarioDto> ObtenerPanoramaInventarioAsync();

        // Panorama gráfico de Alquileres: mismo criterio que los otros, sobre una
        // ventana fija de meses hacia atrás (el mes actual completo incluido).
        Task<PanoramaAlquileresDto> ObtenerPanoramaAlquileresAsync();
    }
}
