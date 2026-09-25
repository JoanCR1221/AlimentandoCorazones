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

    // Un valor agregado por categoría de beneficiario (ver CategoriasBeneficiario).
    public sealed record ConteoPorCategoriaDto(string Categoria, int Cantidad);

    // Un valor agregado por mes calendario. Mes va de 1 a 12; el par (Anio, Mes)
    // identifica el período sin ambigüedad entre años distintos.
    public sealed record ConteoPorMesDto(int Anio, int Mes, int Cantidad);

    // Igual que ConteoPorMesDto pero con el tiempo de comida como tercera
    // dimensión: de una sola consulta agrupada por (año, mes, tiempo de comida)
    // salen tanto el total por tiempo de comida como la tendencia mensual del
    // panorama, sin repetir la consulta.
    public sealed record ConteoComidaMensualDto(int Anio, int Mes, string TiempoComida, int Cantidad);

    // Cifras del panorama gráfico de Beneficiarios: a diferencia de
    // ReporteBeneficiariosDto no depende de los filtros de una pantalla, es el
    // mismo panorama para cualquiera que lo abra, sobre una ventana fija de
    // meses hacia atrás (ver ReportesService.ObtenerPanoramaBeneficiariosAsync).
    public class PanoramaBeneficiariosDto
    {
        public IReadOnlyList<ConteoPorCategoriaDto> BeneficiariosPorCategoria { get; set; } = Array.Empty<ConteoPorCategoriaDto>();
        public int Activos { get; set; }
        public int Inactivos { get; set; }
        public IReadOnlyList<ConteoPorMesDto> AltasPorMes { get; set; } = Array.Empty<ConteoPorMesDto>();
        public int Desayunos { get; set; }
        public int Almuerzos { get; set; }
        public int Meriendas { get; set; }
        public IReadOnlyList<ConteoPorMesDto> ComidasPorMes { get; set; } = Array.Empty<ConteoPorMesDto>();
        public IReadOnlyList<ConteoPorMesDto> PersonasAtendidasPorMes { get; set; } = Array.Empty<ConteoPorMesDto>();
    }
}
