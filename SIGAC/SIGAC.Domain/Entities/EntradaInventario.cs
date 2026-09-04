namespace SIGAC.Domain.Entities
{
    public class EntradaInventario
    {
        public int Id { get; set; }
        public int ArticuloId { get; set; }
        public Articulo? Articulo { get; set; }
        public int Cantidad { get; set; }
        public DateTime Fecha { get; set; }
        public string Origen { get; set; } = string.Empty; // "Donacion" o "Compra"
        public int? DonanteId { get; set; }
        public int? GastoOperativoId { get; set; }
        public string? Observaciones { get; set; }
    }
}