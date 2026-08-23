namespace SIGAC.Domain.Entities
{
    public class SalidaInventario
    {
        public int Id { get; set; }
        public int ArticuloId { get; set; }
        public Articulo? Articulo { get; set; }
        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public string TipoSalida { get; set; } = string.Empty; // "Donacion" o "Prestamo"
        public string? ComunidadDestinataria { get; set; }
        public int? SolicitudPrestamoId { get; set; }
        public string? Observaciones { get; set; }
    }
}