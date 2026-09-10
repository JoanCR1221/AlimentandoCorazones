namespace SIGAC.Domain.Entities
{
    public enum EstadoGastoOperativo
    {
        Activo,
        Anulado
    }

    // Borrador mínimo: la creación/configuración definitiva de esta entidad es
    // tarea de "Base de datos" (AB#2489/2491/2494), todavía sin empezar. Se
    // define acá solo para poder avanzar con el DTO/servicio/repositorio del
    // lado de Backend mientras tanto, con el mismo criterio que Beneficiario y
    // Articulo al principio de sus módulos.
    public class GastoOperativo
    {
        public int Id { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public DateTime Fecha { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string Responsable { get; set; } = string.Empty;
        public EstadoGastoOperativo Estado { get; set; } = EstadoGastoOperativo.Activo;
        public DateTime FechaRegistro { get; set; }
        public string? MotivoAnulacion { get; set; }
    }
}
