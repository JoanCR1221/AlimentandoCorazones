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
        public string? Codigo { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string UnidadMedida { get; set; } = string.Empty;
        public string? Ubicacion { get; set; }
        public int StockActual { get; set; }
        public bool StockBajo { get; set; }
    }

    public class FiltrosExistenciaDto
    {
        public const int TamanoPaginaPredeterminado = 20;

        // Techo duro: ningún llamador puede pedir una página tan grande que anule
        // la paginación y traiga el catálogo entero.
        public const int TamanoPaginaMaximo = 100;

        // Busca por Nombre o por Código en la misma caja: quien tiene el artículo
        // en la mano puede escribir cualquiera de los dos.
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }

        // Base 0, igual que el índice de página de la grilla. El repositorio la
        // resuelve en SQL con Skip/Take: nunca se traen los registros anteriores.
        public int Pagina { get; set; }
        public int TamanoPagina { get; set; } = TamanoPaginaPredeterminado;

        // Valores saneados: el repositorio usa estos, no los crudos, para que una
        // página negativa o un tamaño de 0 no rompan el Skip/Take.
        public int PaginaEfectiva => Pagina < 0 ? 0 : Pagina;

        public int TamanoPaginaEfectivo => Math.Clamp(
            TamanoPagina <= 0 ? TamanoPaginaPredeterminado : TamanoPagina,
            1,
            TamanoPaginaMaximo);
    }

    public class ArticuloEditarDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        // Opcional: no todo artículo tiene un código asignado.
        public string? Codigo { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        public string Categoria { get; set; } = string.Empty;

        [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
        public string UnidadMedida { get; set; } = string.Empty;

        public string? Ubicacion { get; set; }

        // Antes fijo en 5 para todo artículo (el valor por defecto de la entidad);
        // ahora se puede ajustar por artículo desde acá.
        [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo no puede ser negativo.")]
        public int StockMinimo { get; set; }

        // Solo informativo: se muestra de solo lectura, no se edita desde acá.
        public int StockActual { get; set; }
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