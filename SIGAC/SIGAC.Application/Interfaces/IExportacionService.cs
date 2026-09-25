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
    // Inventario, Gastos) no necesita tocar este servicio.
    public interface IExportacionService
    {
        Task<byte[]> ExportarPDFAsync<T>(IEnumerable<T> datos, string titulo);

        Task<byte[]> ExportarExcelAsync<T>(IEnumerable<T> datos, string titulo);
    }
}
