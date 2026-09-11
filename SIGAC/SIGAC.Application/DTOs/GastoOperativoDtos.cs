using System.ComponentModel.DataAnnotations;

namespace SIGAC.Application.DTOs.Gastos
{
    public class GastoOperativoCrearDto
    {
        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string Categoria { get; set; } = string.Empty;

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal Monto { get; set; }

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
    public sealed record GastosConsultaDto(IReadOnlyList<GastoOperativoListaDto> Gastos, decimal TotalAcumulado);

    public class AnulacionGastoDto
    {
        public int GastoId { get; set; }

        [Required(ErrorMessage = "El motivo de anulación es obligatorio.")]
        public string MotivoAnulacion { get; set; } = string.Empty;
    }
}
