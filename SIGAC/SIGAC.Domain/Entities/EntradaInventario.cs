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

        // Se anula (no se borra) cuando el gasto operativo vinculado se anula:
        // el movimiento es el respaldo contable y no puede desaparecer del
        // historial, mismo criterio que el Restrict de sus FK.
        public bool Anulada { get; set; }
        public string? MotivoAnulacion { get; set; }
    }
}