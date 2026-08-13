namespace SIGAC.Application.DTOs.Beneficiarios
{
    public class BeneficiarioCrearDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public bool Estado { get; set; }
        public DateTime FechaRegistro { get; set; }
    }

    public class BeneficiarioEditarDto
    {
        public string Nombre { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
    }

    public class BeneficiarioListaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime FechaNacimiento { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public bool Estado { get; set; }
    }

    public class FiltrosBeneficiarioDto
    {
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }
        public bool? Estado { get; set; }
    }
}