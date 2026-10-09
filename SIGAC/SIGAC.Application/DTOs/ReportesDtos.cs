using System.ComponentModel.DataAnnotations;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.DTOs.Reportes
{
    // Una línea del resumen que se imprime al pie de un reporte exportado
    // ("Total de asistencias en el período" / "12"). El valor ya viene como
    // texto: lo formatea la pantalla que conoce monedas y unidades, no el
    // exportador.
    public sealed record LineaResumenReporte(string Etiqueta, string Valor);

    // Una fila del reporte de beneficiarios atendidos: un beneficiario con sus
    // asistencias del período, separadas por tiempo de comida (PBI 1940).
    //
    // El exportador genérico convierte cada propiedad pública en una columna y
    // lee el título de [Display(Name)]; con AutoGenerateField = false la
    // propiedad no se exporta. La pantalla ignora estos atributos.
    public class ReporteBeneficiariosDto
    {
        [Display(AutoGenerateField = false)]
        public int BeneficiarioId { get; set; }

        [Display(Name = "Nombre")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Display(Name = "Categoría")]
        public string Categoria { get; set; } = string.Empty;

        public int Desayunos { get; set; }
        public int Almuerzos { get; set; }
        public int Meriendas { get; set; }

        [Display(Name = "Total")]
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

    // Una fila del reporte de alquileres de espacios (PBI B). Horario y Sectores ya
    // vienen como texto: el exportador convierte cada propiedad pública en una
    // columna y no formatea listas ni franjas. Sin Id por lo mismo que los otros
    // reportes. Los cancelados se listan (con su Estado) pero no suman a los
    // ingresos ni a las horas.
    public class ReporteAlquileresDto
    {
        public DateTime Fecha { get; set; }

        // "8:00 a. m. – 12:00 p. m.", el mismo formato del calendario.
        public string Horario { get; set; } = string.Empty;

        public string Arrendatario { get; set; } = string.Empty;

        // Los sectores del alquiler, separados por coma.
        public string Sectores { get; set; } = string.Empty;

        public int Personas { get; set; }

        public decimal Monto { get; set; }

        public string Moneda { get; set; } = string.Empty;

        // "Reservado" o "Cancelado" (EstadoAlquiler).
        public string Estado { get; set; } = string.Empty;
    }

    // Sin acotar es "desde siempre" / "hasta hoy", todos los sectores y ambos
    // estados, igual que el calendario de alquileres. EspacioId trae los alquileres
    // que usan ese sector, entre otros.
    public class FiltrosReporteAlquileresDto
    {
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public int? EspacioId { get; set; }
        public EstadoAlquiler? Estado { get; set; }
    }

    // Las filas y lo que piden los criterios del PBI: alquileres reservados y
    // cancelados, las horas alquiladas y el ingreso por moneda (sin sumar monedas
    // distintas). Ingresos y horas cuentan solo los reservados.
    public class ReporteAlquileresResultadoDto
    {
        public IReadOnlyList<ReporteAlquileresDto> Filas { get; set; } = Array.Empty<ReporteAlquileresDto>();
        public int CantidadReservados { get; set; }
        public int CantidadCancelados { get; set; }

        // Suma de la duración de cada alquiler reservado, contado una sola vez
        // aunque use varios sectores: mide cuánto tiempo se usó el local, no
        // sectores-hora.
        public decimal HorasAlquiladas { get; set; }

        public IReadOnlyList<MontoPorMonedaDto> IngresosPorMoneda { get; set; } = Array.Empty<MontoPorMonedaDto>();
    }

    // Un alquiler reducido a lo que necesita el panorama de alquileres: cuándo fue,
    // cuánto duró, cuánto costó y qué sectores usó. Lo trae el repositorio ya
    // acotado a la ventana de meses y el servicio lo agrupa.
    public sealed record AlquilerPanoramaDto(
        DateTime Fecha,
        TimeSpan HoraInicio,
        TimeSpan HoraFin,
        decimal Monto,
        string Moneda,
        EstadoAlquiler Estado,
        IReadOnlyList<string> Sectores);

    // Horas alquiladas en un mes calendario.
    public sealed record HorasPorMesDto(int Anio, int Mes, decimal Horas);

    // Cuántos alquileres reservados usaron un sector. Un alquiler de dos sectores
    // cuenta en los dos.
    public sealed record AlquileresPorSectorDto(string Sector, int Cantidad);

    // Cifras del panorama gráfico de Alquileres: mismo criterio que
    // PanoramaGastosDto, una ventana fija de meses hacia atrás que no depende de
    // filtros de pantalla (ver ReportesService.ObtenerPanoramaAlquileresAsync). Todo
    // lo que mide uso o ingreso cuenta solo los alquileres reservados: uno cancelado
    // no ocupa el local ni genera ingreso. El ingreso va solo en colones, porque no
    // se pueden sumar monedas distintas.
    public sealed class PanoramaAlquileresDto
    {
        public int CantidadReservados { get; set; }
        public int CantidadCancelados { get; set; }
        public decimal HorasAlquiladas { get; set; }
        public decimal IngresosEnColones { get; set; }
        public IReadOnlyList<ConteoPorMesDto> ReservadosPorMes { get; set; } = Array.Empty<ConteoPorMesDto>();
        public IReadOnlyList<ConteoPorMesDto> CanceladosPorMes { get; set; } = Array.Empty<ConteoPorMesDto>();
        public IReadOnlyList<MontoPorMesDto> IngresosEnColonesPorMes { get; set; } = Array.Empty<MontoPorMesDto>();
        public IReadOnlyList<HorasPorMesDto> HorasPorMes { get; set; } = Array.Empty<HorasPorMesDto>();
        public IReadOnlyList<AlquileresPorSectorDto> AlquileresPorSector { get; set; } = Array.Empty<AlquileresPorSectorDto>();
        public int ReservadosEntreSemana { get; set; }
        public int ReservadosFinDeSemana { get; set; }
    }

    // Cantidad de donaciones recibidas en un mes, separada por clase ("Dinero" o
    // "Especie", los valores de DonacionesService.TipoDonacionDinero/Especie): de
    // una sola consulta salen el total por clase y la tendencia mensual del
    // panorama, igual que ConteoComidaMensualDto con los tiempos de comida.
    public sealed record ConteoDonacionMensualDto(int Anio, int Mes, string TipoDonacion, int Cantidad);

    // Cuántas donaciones (de dinero y de especie juntas) hizo un donante. Se cuenta
    // y no se suma dinero porque hay donantes que solo donan en especie o en otra
    // moneda, y un ranking por monto los dejaría fuera.
    public sealed record DonacionesPorDonanteDto(string Donante, int Cantidad);

    // Cifras del panorama gráfico de Donaciones: mismo criterio que
    // PanoramaGastosDto, una ventana fija de meses hacia atrás que no depende de
    // filtros de pantalla (ver ReportesService.ObtenerPanoramaDonacionesAsync).
    // El dinero va solo en colones, como en gastos: sumar monedas distintas no
    // representa nada. Los donantes activos e inactivos son el total actual, no el
    // de la ventana.
    public sealed class PanoramaDonacionesDto
    {
        public int CantidadDinero { get; set; }
        public int CantidadEspecie { get; set; }
        public IReadOnlyList<ConteoDonacionMensualDto> DonacionesPorMes { get; set; } = Array.Empty<ConteoDonacionMensualDto>();
        public IReadOnlyList<MontoPorMesDto> DineroEnColonesPorMes { get; set; } = Array.Empty<MontoPorMesDto>();
        public IReadOnlyList<DonacionesPorDonanteDto> TopDonantes { get; set; } = Array.Empty<DonacionesPorDonanteDto>();
        public int DonantesActivos { get; set; }
        public int DonantesInactivos { get; set; }
    }

    // Una fila del reporte de donaciones recibidas: dinero y especie en la misma
    // tabla, igual que el historial de donaciones (PBI 1939). Sin Id: el del
    // historial es el de su tabla y se repetiría entre dinero y especie, y al
    // exportador no le sirve de columna.
    public class ReporteDonacionesDto
    {
        public DateTime Fecha { get; set; }

        [Display(Name = "Tipo")]
        public string TipoDonacion { get; set; } = string.Empty;

        public string Donante { get; set; } = string.Empty;

        // Nullable y sin valor en las donaciones en especie, que no se valorizan:
        // la celda queda vacía en vez de mostrar un 0 que se leería como "donó cero".
        public decimal? Monto { get; set; }

        public string? Moneda { get; set; }

        [Display(Name = "Descripción")]
        public string Descripcion { get; set; } = string.Empty;
    }

    // TipoDonacion en null significa "ambas" (Dinero y Especie); las fechas sin
    // acotar son "desde siempre" / "hasta hoy", igual que en los otros reportes.
    public class FiltrosReporteDonacionesDto
    {
        public string? TipoDonacion { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }

    // Las filas y lo que piden los criterios del PBI: la cantidad de donaciones
    // de cada clase y el total de dinero. El total va POR MONEDA y nunca como un
    // solo número, porque colones, dólares y euros no se suman entre sí; las
    // donaciones en especie no aportan monto.
    public class ReporteDonacionesResultadoDto
    {
        public IReadOnlyList<ReporteDonacionesDto> Filas { get; set; } = Array.Empty<ReporteDonacionesDto>();
        public int CantidadDinero { get; set; }
        public int CantidadEspecie { get; set; }
        public IReadOnlyList<MontoPorMonedaDto> TotalesPorMoneda { get; set; } = Array.Empty<MontoPorMonedaDto>();
    }
}
