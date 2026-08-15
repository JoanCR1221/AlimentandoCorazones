using SIGAC.Domain;

namespace SIGAC.Domain.Entities
{
    public class Beneficiario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public bool Estado { get; set; } = true;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;
        public string? TipoDocumento { get; set; }
        public string? NumIdentidad { get; set; }
        public string? TipoDocumentoOtro { get; set; }
    }
}