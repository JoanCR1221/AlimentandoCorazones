using System.ComponentModel.DataAnnotations;

namespace SIGAC.Application.DTOs.Inventario
{
    public class EntradaInventarioCrearDto
    {
        [Required(ErrorMessage = "El nombre del artículo es obligatorio.")]
        public string NombreArticulo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string Categoria { get; set; } = string.Empty;

        [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
        public string UnidadMedida { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        // Fecha queda sin [Required]: DateTime no-nullable siempre "tiene valor"
        // para DataAnnotations, así que la validación no dispararía nunca.
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "El origen es obligatorio.")]
        public string Origen { get; set; } = string.Empty;

        public int? DonanteId { get; set; }
        public int? GastoOperativoId { get; set; }
        public string? Observaciones { get; set; }
    }

    public class SalidaDonacionCrearDto
    {
        public int ArticuloId { get; set; }
        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public string ComunidadDestinataria { get; set; } = string.Empty;
        public string? Observaciones { get; set; }
    }

    public class ArticuloExistenciaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string UnidadMedida { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public bool StockBajo { get; set; }
    }

    public class FiltrosExistenciaDto
    {
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }
    }

    public class ArticuloEditarDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string UnidadMedida { get; set; } = string.Empty;
    }

    public class MovimientoInventarioDto
    {
        public int Id { get; set; }
        public string Articulo { get; set; } = string.Empty;
        public string TipoMovimiento { get; set; } = string.Empty; // "Entrada" o "Salida"
        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public string? OrigenODestino { get; set; }
    }

    public class FiltrosMovimientoDto
    {
        public int? ArticuloId { get; set; }
        public string? TipoMovimiento { get; set; } // "Entrada", "Salida" o null (ambos)
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
    }

    public class HistorialMovimientosResultadoDto
    {
        public List<MovimientoInventarioDto> Movimientos { get; set; } = new();
        public int TotalEntradas { get; set; }
        public int TotalSalidas { get; set; }
    }

    public class SolicitudPrestamoCrearDto
    {
        public int ArticuloId { get; set; }
        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public string Actividad { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
    }

    public class ResolucionPrestamoDto
    {
        public int SolicitudId { get; set; }
        public bool Aprobado { get; set; }
        public string? MotivoRechazo { get; set; }
    }

    public class SolicitudPrestamoListaDto
    {
        public int Id { get; set; }
        public string Articulo { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public string Actividad { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }
}