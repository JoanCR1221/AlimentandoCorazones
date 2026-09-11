namespace SIGAC.Domain.Entities
{
    public enum EstadoGastoOperativo
    {
        Activo,
        Anulado
    }

    // Entidad del módulo de Gastos Operativos, ya configurada en SigacDbContext
    // y respaldada por migración (AB#2489/2491/2494). Categoria y Estado son
    // dominios cerrados: los sostienen CategoriasGastoOperativo y los CHECK
    // CK_GastosOperativos_Categoria / CK_GastosOperativos_Estado.
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
