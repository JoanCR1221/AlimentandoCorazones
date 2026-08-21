namespace SIGAC.Domain.Entities
{
    public class AsistenciaComedor
    {
        public int Id { get; set; }
        public int BeneficiarioId { get; set; }
        public Beneficiario? Beneficiario { get; set; }
        public DateTime Fecha { get; set; }
        public string TiempoComida { get; set; } = string.Empty; // Desayuno, Almuerzo, Merienda
    }
}