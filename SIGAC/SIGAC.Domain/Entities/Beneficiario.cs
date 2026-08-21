using SIGAC.Domain;

namespace SIGAC.Domain.Entities
{
    public class Beneficiario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string PrimerApellido { get; set; } = string.Empty;

        // Los extranjeros suelen tener un solo apellido. Se guarda como cadena
        // vacía y nunca como NULL: en SQL Server dos NULL no se consideran iguales,
        // y el índice único de (Nombre, apellidos, FechaNacimiento) no los detectaría.
        public string SegundoApellido { get; set; } = string.Empty;

        // Solo para mostrar en listados: no se mapea a la base de datos.
        public string NombreCompleto =>
            ReglasBeneficiario.ComponerNombreCompleto(Nombre, PrimerApellido, SegundoApellido);

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
