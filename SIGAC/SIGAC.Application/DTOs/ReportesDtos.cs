namespace SIGAC.Application.DTOs.Reportes
{
    // Una fila del reporte de beneficiarios atendidos: un beneficiario con sus
    // asistencias del período, separadas por tiempo de comida (PBI 1940).
    public class ReporteBeneficiariosDto
    {
        public int BeneficiarioId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int Desayunos { get; set; }
        public int Almuerzos { get; set; }
        public int Meriendas { get; set; }
        public int TotalAsistencias { get; set; }
    }

    // Categoria en null o vacío significa "todas". Igual con las fechas: sin
    // acotar es "desde siempre" / "hasta hoy".
    public class FiltrosReporteBeneficiariosDto
    {
        public string? Categoria { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }

    // Las filas y el total general pedido por el criterio de aceptación del
    // PBI ("se muestra el total de asistencias registradas en el período").
    public class ReporteBeneficiariosResultadoDto
    {
        public IReadOnlyList<ReporteBeneficiariosDto> Filas { get; set; } = Array.Empty<ReporteBeneficiariosDto>();
        public int TotalGeneral { get; set; }
    }
}
