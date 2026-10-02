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

    // Montos en colones agrupados por una dimensión del gasto. Solo colones:
    // sumar monedas distintas no tiene sentido (ver ResumenGastosDto). El monto
    // es MontoSinIva + Iva, igual que el resto del sistema: lo que de verdad
    // salió de la cuenta.
    public sealed record MontoPorTipoDto(string TipoGasto, decimal Monto);
    public sealed record MontoPorFormaPagoDto(string FormaPago, decimal Monto);
    public sealed record MontoPorMesDto(int Anio, int Mes, decimal Monto);
    public sealed record MontoPorProveedorDto(string Proveedor, decimal Monto);

    // Cifras del panorama gráfico de Gastos Operativos: mismo criterio que
    // PanoramaBeneficiariosDto, una ventana fija de meses hacia atrás que no
    // depende de filtros de pantalla (ver ReportesService.ObtenerPanoramaGastosAsync).
    // Sugerido por el cliente, sin PBI propio: no reemplaza al reporte exportable
    // de gastos, que sigue pendiente aparte.
    public sealed class PanoramaGastosDto
    {
        public IReadOnlyList<MontoPorTipoDto> MontoPorTipo { get; set; } = Array.Empty<MontoPorTipoDto>();
        public IReadOnlyList<MontoPorFormaPagoDto> MontoPorFormaPago { get; set; } = Array.Empty<MontoPorFormaPagoDto>();
        public IReadOnlyList<MontoPorMesDto> MontoPorMes { get; set; } = Array.Empty<MontoPorMesDto>();
        public IReadOnlyList<ConteoPorMesDto> CantidadPorMes { get; set; } = Array.Empty<ConteoPorMesDto>();
        public IReadOnlyList<MontoPorProveedorDto> TopProveedores { get; set; } = Array.Empty<MontoPorProveedorDto>();
        public int Activos { get; set; }
        public int Anulados { get; set; }
    }

    // Mes, año y forma de pago del reporte contable de gastos: las tres
    // columnas que identifican "qué mes de qué libro" para la contadora
    // ("DETALLE DE GASTOS MES DE" / "GASTOS DE <FormaPago>" en el papel).
    // A diferencia de FiltrosReporteBeneficiariosDto, acá nada es opcional: el
    // reporte en papel siempre es de un mes y una forma de pago a la vez.
    public class FiltrosReporteGastosDto
    {
        public int Mes { get; set; }
        public int Anio { get; set; }
        public string FormaPago { get; set; } = string.Empty;
    }

    // Una fila del reporte: un gasto dentro de su grupo (tipo de gasto +
    // descripción de cuenta). Monto e Iva van por separado y no sumados: el
    // papel de la contadora los reporta así, columna por columna.
    public sealed record FilaReporteGastosDto(
        string Proveedor,
        string NumeroFactura,
        int Dia,
        decimal Monto,
        decimal Iva,
        string? NumeroCheque,
        string CuentaContable);

    // Un grupo del reporte: "POR TIPO DE GASTO Y DESCRIPCION CUENTA" en el
    // encabezado del papel es justo esto. El subtotal es la fila "TOTAL . . ."
    // que cierra cada grupo.
    public sealed class GrupoReporteGastosDto
    {
        public string TipoGasto { get; set; } = string.Empty;
        public string DescripcionCuenta { get; set; } = string.Empty;
        public IReadOnlyList<FilaReporteGastosDto> Filas { get; set; } = Array.Empty<FilaReporteGastosDto>();
        public decimal SubtotalMonto { get; set; }
        public decimal SubtotalIva { get; set; }
    }

    // El reporte completo: los grupos, en el mismo orden en que se imprimirían,
    // y el "GRAN TOTAL . . ." final.
    public sealed class ReporteGastosDto
    {
        public IReadOnlyList<GrupoReporteGastosDto> Grupos { get; set; } = Array.Empty<GrupoReporteGastosDto>();
        public decimal GranTotalMonto { get; set; }
        public decimal GranTotalIva { get; set; }
    }
}
