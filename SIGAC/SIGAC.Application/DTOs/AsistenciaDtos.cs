using System.ComponentModel.DataAnnotations;

namespace SIGAC.Application.DTOs.Asistencia
{
    public class AsistenciaCrearDto
    {
        public int BeneficiarioId { get; set; }
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "El tiempo de comida es obligatorio.")]
        public string TiempoComida { get; set; } = string.Empty;
    }

    public class HistorialAsistenciaDto
    {
        public int Id { get; set; }
        public string NombreBeneficiario { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string TiempoComida { get; set; } = string.Empty;
    }

    public class FiltrosAsistenciaDto
    {
        public int? BeneficiarioId { get; set; }
        public string? TiempoComida { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }

    // Envuelve el historial + los totales pedidos en la historia de usuario
    public class HistorialAsistenciaResultadoDto
    {
        public List<HistorialAsistenciaDto> Registros { get; set; } = new();
        public Dictionary<string, int> TotalesPorBeneficiario { get; set; } = new();
        public Dictionary<string, int> TotalesPorTiempoComida { get; set; } = new();
    }
}