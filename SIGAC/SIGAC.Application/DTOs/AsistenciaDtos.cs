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
        public const int TamanoPaginaPredeterminado = 20;

        // Techo duro: ningún llamador puede pedir una página tan grande que anule
        // la paginación y traiga el historial entero (crece con cada día de servicio).
        public const int TamanoPaginaMaximo = 100;

        public int? BeneficiarioId { get; set; }
        public string? TiempoComida { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

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

    // Una página del historial más lo que la pantalla muestra del período completo.
    // Los totales cubren TODOS los registros que cumplen los filtros, no solo los de
    // la página: salen de un agregado en la base, no de contar las filas de Registros.
    public class HistorialAsistenciaResultadoDto
    {
        public List<HistorialAsistenciaDto> Registros { get; set; } = new();

        // Total de registros que cumplen los filtros; lo necesita el paginador para
        // saber cuántas páginas hay sin traerlas.
        public int TotalRegistros { get; set; }

        public Dictionary<string, int> TotalesPorTiempoComida { get; set; } = new();
    }
}