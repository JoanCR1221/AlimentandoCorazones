namespace SIGAC.Domain.Entities
{
    public enum EstadoSolicitudPrestamo
    {
        Pendiente,
        Aprobada,
        Rechazada
    }

    public class SolicitudPrestamo
    {
        public int Id { get; set; }
        public int ArticuloId { get; set; }
        public Articulo? Articulo { get; set; }
        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public string Actividad { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public EstadoSolicitudPrestamo Estado { get; set; } = EstadoSolicitudPrestamo.Pendiente;
        public string? MotivoRechazo { get; set; }

        // Nullable a propósito: una solicitud en Pendiente todavía no fue resuelta y
        // por lo tanto no tiene fecha que registrar. Se llena al aprobar o rechazar.
        public DateTime? FechaResolucion { get; set; }
    }
}