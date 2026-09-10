namespace SIGAC.Domain.Entities
{
    // Donación recibida en efectivo o transferencia. No toca el inventario: el
    // dinero no es un artículo con stock, así que esta entidad se guarda sola, sin
    // EntradaInventario asociada.
    public class DonacionDinero
    {
        public int Id { get; set; }

        // Obligatorio (no nullable): una donación de dinero siempre se registra a
        // nombre de alguien, aunque ese alguien sea un donante genérico "Anónimo".
        public int DonanteId { get; set; }
        public Donante? Donante { get; set; }

        public decimal Monto { get; set; }
        public DateTime Fecha { get; set; }
        public string? Observaciones { get; set; }
    }
}
