using System.ComponentModel.DataAnnotations;

namespace SIGAC.Application.DTOs.Gastos
{
    public class GastoOperativoCrearDto
    {
        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string Categoria { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal Monto { get; set; }

        // Valor del catálogo cerrado TiposMoneda. Sin ella, un Monto de 100 no
        // dice si son 100 colones o 100 dólares.
        [Required(ErrorMessage = "La moneda es obligatoria.")]
        public string Moneda { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        public string Descripcion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El responsable es obligatorio.")]
        public string Responsable { get; set; } = string.Empty;
    }

    public class GastoOperativoEditarDto
    {
        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string Categoria { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "La moneda es obligatoria.")]
        public string Moneda { get; set; } = string.Empty;

        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        public string Descripcion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El responsable es obligatorio.")]
        public string Responsable { get; set; } = string.Empty;
    }

    public class GastoOperativoListaDto
    {
        public int Id { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string Moneda { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }

    public class FiltrosGastoDto
    {
        public string? Categoria { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }

    // El total es del conjunto filtrado completo, no de lo que se ve en
    // pantalla: por eso viaja junto con la lista en vez de calcularse en el
    // frontend, que solo vería una posible futura página.
    //
    // Un total POR MONEDA y no un solo decimal: desde que el gasto declara en
    // qué moneda se pagó, sumar colones con dólares en un único número dejaría
    // de representar nada.
    public sealed record GastosConsultaDto(IReadOnlyList<GastoOperativoListaDto> Gastos, IReadOnlyList<MontoPorMonedaDto> TotalesPorMoneda);

    public class AnulacionGastoDto
    {
        public int GastoId { get; set; }

        [Required(ErrorMessage = "El motivo de anulación es obligatorio.")]
        public string MotivoAnulacion { get; set; } = string.Empty;
    }
}
