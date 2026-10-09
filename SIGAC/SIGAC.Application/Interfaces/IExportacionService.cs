using SIGAC.Application.DTOs.Reportes;

namespace SIGAC.Application.Interfaces
{
    // Exportación genérica de reportes a PDF y Excel (módulo de Generación de
    // reportes). Application no conoce FastReport: solo entra una lista de datos
    // ya proyectada (el DTO de fila del reporte) y sale el archivo listo para
    // descargar. La implementación vive en Infrastructure porque depende del
    // paquete de terceros, mismo criterio que IUsuariosService con Identity.
    //
    // Genérica y no una por tipo de reporte: arma las columnas reflexionando
    // sobre las propiedades públicas de T, así que un reporte nuevo (Donaciones,
    // Inventario) no necesita tocar este servicio.
    //
    // Subtitulo (período o filtros usados) y resumen (totales) son opcionales: se
    // imprimen bajo el título y al pie del archivo. Los títulos de columna salen de
    // [Display(Name)] en el DTO de fila, y AutoGenerateField = false oculta una
    // propiedad (por ejemplo un Id).
    public interface IExportacionService
    {
        Task<byte[]> ExportarPDFAsync<T>(IEnumerable<T> datos, string titulo, string? subtitulo = null, IReadOnlyList<LineaResumenReporte>? resumen = null);

        Task<byte[]> ExportarExcelAsync<T>(IEnumerable<T> datos, string titulo, string? subtitulo = null, IReadOnlyList<LineaResumenReporte>? resumen = null);

        // Aparte y no genérico: el reporte de gastos no es una tabla plana, es un
        // documento agrupado con subtotales por grupo y un gran total, igual al
        // que ya usa la contadora, con bandas de grupo de FastReport.
        Task<byte[]> ExportarReporteGastosPDFAsync(ReporteGastosDto reporte, int mes, int anio, string formaPago);

        // Mismo documento que ExportarReporteGastosPDFAsync, en Excel: tampoco
        // encaja en ExportarExcelAsync<T> porque no es una tabla plana.
        Task<byte[]> ExportarReporteGastosExcelAsync(ReporteGastosDto reporte, int mes, int anio, string formaPago);
    }
}
