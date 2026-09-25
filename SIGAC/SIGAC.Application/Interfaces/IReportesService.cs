using SIGAC.Application.DTOs.Reportes;

namespace SIGAC.Application.Interfaces
{
    // Reportes institucionales consolidados (módulo de Generación de reportes).
    // Un método por reporte: Donaciones, Inventario y Gastos se agregan acá
    // mismo cuando les toque, sin una interfaz nueva por cada uno.
    public interface IReportesService
    {
        Task<ReporteBeneficiariosResultadoDto> GenerarReporteBeneficiariosAsync(FiltrosReporteBeneficiariosDto filtros);
    }
}
