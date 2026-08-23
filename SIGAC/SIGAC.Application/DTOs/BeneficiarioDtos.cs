using SIGAC.Domain;
using System.ComponentModel.DataAnnotations;

namespace SIGAC.Application.DTOs.Beneficiarios
{
    public class BeneficiarioCrearDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El primer nombre es obligatorio.")]
        public string PrimerNombre { get; set; } = string.Empty;

        // Opcional: se guarda como cadena vacía cuando no se ingresa.
        public string SegundoNombre { get; set; } = string.Empty;

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
        [Required(ErrorMessage = "El primer nombre es obligatorio.")]
        public string PrimerNombre { get; set; } = string.Empty;

        public string SegundoNombre { get; set; } = string.Empty;

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
        public string PrimerNombre { get; set; } = string.Empty;
        public string SegundoNombre { get; set; } = string.Empty;
        public string PrimerApellido { get; set; } = string.Empty;
        public string SegundoApellido { get; set; } = string.Empty;

        public string NombreCompleto =>
            ReglasBeneficiario.ComponerNombreCompleto(PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido);

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
        public const int TamanoPaginaPredeterminado = 20;

        // Mínimo para que un autocompletado dispare la búsqueda. Con menos, el
        // desplegable mostraría una lista arbitraria y parecería un combo en vez
        // de un buscador.
        public const int MinimoCaracteresBusqueda = 2;

        // Techo duro: ningún llamador puede pedir una página tan grande que anule
        // la paginación y traiga la tabla entera.
        public const int TamanoPaginaMaximo = 100;

        // Texto libre: se busca en los dos nombres, en los dos apellidos y en el
        // número de identidad, todo en la misma caja. En el mostrador llega alguien
        // con la cédula en la mano y se busca por el número sin cambiar de campo.
        public string? Nombre { get; set; }
        public string? Categoria { get; set; }
        public string? TipoDocumento { get; set; }
        public bool? Estado { get; set; }

        // Base 0, igual que el índice de página de la grilla. El repositorio la
        // resuelve en SQL con Skip/Take: nunca se traen los registros anteriores.
        public int Pagina { get; set; }
        public int TamanoPagina { get; set; } = TamanoPaginaPredeterminado;

        // Valores saneados: el repositorio usa estos, no los crudos, para que una
        // página negativa o un tamaño de 0 no rompan el Skip/Take.
        public int PaginaEfectiva => Pagina < 0 ? 0 : Pagina;

        public int TamanoPaginaEfectivo => Math.Clamp(
            TamanoPagina <= 0 ? TamanoPaginaPredeterminado : TamanoPagina,
            1,
            TamanoPaginaMaximo);
    }
}
