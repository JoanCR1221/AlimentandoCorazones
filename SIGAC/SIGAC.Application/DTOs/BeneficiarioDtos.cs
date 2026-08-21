using SIGAC.Domain;
using System.ComponentModel.DataAnnotations;

namespace SIGAC.Application.DTOs.Beneficiarios
{
    public class BeneficiarioCrearDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El primer apellido es obligatorio.")]
        public string PrimerApellido { get; set; } = string.Empty;

        // Opcional: se guarda como cadena vacía cuando no se ingresa.
        public string SegundoApellido { get; set; } = string.Empty;

        public DateTime FechaNacimiento { get; set; }

        // La categoría ya no se captura: la deriva el servicio desde FechaNacimiento.

        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public bool Estado { get; set; }
        public DateTime FechaRegistro { get; set; }

        public string? TipoDocumento { get; set; }
        public string? NumIdentidad { get; set; }
        public string? TipoDocumentoOtro { get; set; }

    }

    public class BeneficiarioEditarDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El primer apellido es obligatorio.")]
        public string PrimerApellido { get; set; } = string.Empty;

        public string SegundoApellido { get; set; } = string.Empty;

        public DateTime FechaNacimiento { get; set; }

        public string? Telefono { get; set; }
        public string? Direccion { get; set; }

        // Se capturaban al registrar y no había forma de corregirlos.
        public string? TipoDocumento { get; set; }
        public string? NumIdentidad { get; set; }
        public string? TipoDocumentoOtro { get; set; }
    }

    public class BeneficiarioListaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string PrimerApellido { get; set; } = string.Empty;
        public string SegundoApellido { get; set; } = string.Empty;

        public string NombreCompleto =>
            ReglasBeneficiario.ComponerNombreCompleto(Nombre, PrimerApellido, SegundoApellido);

        public DateTime FechaNacimiento { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public bool Estado { get; set; }
        public string? TipoDocumento { get; set; }
        public string? NumIdentidad { get; set; }
        public string? TipoDocumentoOtro { get; set; }


    }

    public class FiltrosBeneficiarioDto
    {
        // Texto libre: se busca en nombre y en los dos apellidos.
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }
        public bool? Estado { get; set; }
    }
}
