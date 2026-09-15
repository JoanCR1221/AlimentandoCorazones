namespace SIGAC.Application.DTOs.Bitacora
{
    public class BitacoraAccionDto
    {
        public int Id { get; set; }
        public string? UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string? Rol { get; set; }
        public string Accion { get; set; } = string.Empty;
        public string Modulo { get; set; } = string.Empty;
        public string? Detalle { get; set; }
        public DateTime Fecha { get; set; }
    }

    // Filtros del listado de bitácora (PBI 1949): usuario, rol, módulo y rango
    // de fechas, más paginación en el servidor, mismo patrón que
    // FiltrosBeneficiarioDto. La bitácora crece con cada operación del sistema,
    // así que traerla entera nunca es opción.
    public class FiltrosBitacoraDto
    {
        public const int TamanoPaginaPredeterminado = 25;
        public const int TamanoPaginaMaximo = 100;

        public string? UsuarioId { get; set; }
        public string? Rol { get; set; }
        public string? Modulo { get; set; }
        public string? Accion { get; set; }

        // Ambas inclusivas y sin hora: el repositorio convierte FechaHasta en
        // "menor que el día siguiente" para no perder las acciones de esa jornada.
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        public int Pagina { get; set; }
        public int TamanoPagina { get; set; } = TamanoPaginaPredeterminado;

        public int PaginaEfectiva => Pagina < 0 ? 0 : Pagina;

        public int TamanoPaginaEfectivo => Math.Clamp(
            TamanoPagina <= 0 ? TamanoPaginaPredeterminado : TamanoPagina,
            1,
            TamanoPaginaMaximo);
    }
}
