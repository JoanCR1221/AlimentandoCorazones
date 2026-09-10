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
}
