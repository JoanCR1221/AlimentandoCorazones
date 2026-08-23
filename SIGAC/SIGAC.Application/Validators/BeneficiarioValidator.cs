using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Datos de un beneficiario ya validados y normalizados, listos para persistir.
    // El servicio solo los copia a la entidad: no vuelve a limpiar ni a decidir nada.
    public sealed record BeneficiarioValidado(
        string PrimerNombre,
        string SegundoNombre,
        string PrimerApellido,
        string SegundoApellido,
        DateTime FechaNacimiento,
        string? Telefono,
        string? Direccion,
        string TipoDocumento,
        string? NumIdentidad,
        string? TipoDocumentoOtro);

    // Toda la validación de entrada del beneficiario. Las reglas puras (longitudes,
    // cantidades de dígitos, formatos) viven en ReglasBeneficiario; acá se aplican
    // y se traducen a mensajes para el usuario.
    public static class BeneficiarioValidator
    {
        public static BeneficiarioValidado Validar(BeneficiarioCrearDto dto) =>
            Validar(
                dto.PrimerNombre, dto.SegundoNombre, dto.PrimerApellido, dto.SegundoApellido,
                dto.FechaNacimiento, dto.Telefono, dto.Direccion,
                dto.TipoDocumento, dto.NumIdentidad, dto.TipoDocumentoOtro);

        public static BeneficiarioValidado Validar(BeneficiarioEditarDto dto) =>
            Validar(
                dto.PrimerNombre, dto.SegundoNombre, dto.PrimerApellido, dto.SegundoApellido,
                dto.FechaNacimiento, dto.Telefono, dto.Direccion,
                dto.TipoDocumento, dto.NumIdentidad, dto.TipoDocumentoOtro);

        private static BeneficiarioValidado Validar(
            string? primerNombre,
            string? segundoNombre,
            string? primerApellido,
            string? segundoApellido,
            DateTime fechaNacimiento,
            string? telefono,
            string? direccion,
            string? tipoDocumento,
            string? numIdentidad,
            string? tipoDocumentoOtro)
        {
            var (numero, especificacion) = ValidarDocumento(tipoDocumento, numIdentidad, tipoDocumentoOtro);

            return new BeneficiarioValidado(
                ValidarNombreObligatorio(primerNombre, "El primer nombre", ReglasBeneficiario.LongitudMaximaNombre),
                ValidarNombreOpcional(segundoNombre, "El segundo nombre", ReglasBeneficiario.LongitudMaximaNombre),
                ValidarNombreObligatorio(primerApellido, "El primer apellido", ReglasBeneficiario.LongitudMaximaApellido),
                ValidarNombreOpcional(segundoApellido, "El segundo apellido", ReglasBeneficiario.LongitudMaximaApellido),
                ValidarFechaNacimiento(fechaNacimiento),
                ValidarTelefono(telefono),
                ValidarDireccion(direccion),
                tipoDocumento!,
                numero,
                especificacion);
        }

        // Devuelve el valor tal como se guarda: compactado y con la primera letra
        // en mayúscula (ver TextoNormalizador.NormalizarNombre).
        private static string ValidarNombreObligatorio(string? valor, string etiqueta, int longitudMaxima)
        {
            var normalizado = TextoNormalizador.NormalizarNombre(valor);

            if (normalizado.Length == 0)
                throw new ValidationException($"{etiqueta} es obligatorio.");

            if (normalizado.Length < ReglasBeneficiario.LongitudMinimaNombre)
                throw new ValidationException($"{etiqueta} debe tener al menos {ReglasBeneficiario.LongitudMinimaNombre} caracteres.");

            if (normalizado.Length > longitudMaxima)
                throw new ValidationException($"{etiqueta} no puede superar los {longitudMaxima} caracteres.");

            if (!ReglasBeneficiario.TieneFormatoValido(normalizado))
                throw new ValidationException($"{etiqueta} solo puede contener letras, apóstrofes y guiones.");

            return normalizado;
        }

        // Opcional: cuando no se ingresa se guarda como cadena vacía, no como NULL.
        // Es lo que hace funcionar el índice único (dos NULL no son iguales en SQL Server).
        private static string ValidarNombreOpcional(string? valor, string etiqueta, int longitudMaxima)
        {
            var normalizado = TextoNormalizador.NormalizarNombre(valor);

            if (normalizado.Length == 0)
                return string.Empty;

            return ValidarNombreObligatorio(normalizado, etiqueta, longitudMaxima);
        }

        private static DateTime ValidarFechaNacimiento(DateTime fechaNacimiento)
        {
            if (fechaNacimiento == default)
                throw new ValidationException("La fecha de nacimiento es obligatoria.");

            var fecha = fechaNacimiento.Date;

            if (fecha > DateTime.Today)
                throw new ValidationException("La fecha de nacimiento no puede ser futura.");

            if (CategoriasBeneficiario.CalcularEdad(fecha) > ReglasBeneficiario.EdadMaximaAnios)
                throw new ValidationException($"La fecha de nacimiento no es válida: supera los {ReglasBeneficiario.EdadMaximaAnios} años.");

            return fecha;
        }

        // Opcional. Si se ingresa: 8 dígitos exactos, sin guiones ni espacios.
        // Un solo teléfono por beneficiario.
        private static string? ValidarTelefono(string? telefono)
        {
            var valor = telefono?.Trim();

            if (string.IsNullOrEmpty(valor))
                return null;

            if (!ReglasBeneficiario.TieneFormatoTelefono(valor))
                throw new ValidationException(
                    $"El teléfono debe tener exactamente {ReglasBeneficiario.DigitosTelefono} dígitos, sin guiones ni espacios.");

            return valor;
        }

        // Opcional. Admite números porque se usan referencias como
        // "Del parque 150 metros suroeste, casa verde".
        private static string? ValidarDireccion(string? direccion)
        {
            var valor = TextoNormalizador.CompactarEspacios(direccion);

            if (valor.Length == 0)
                return null;

            if (valor.Length > ReglasBeneficiario.LongitudMaximaDireccion)
                throw new ValidationException(
                    $"La dirección no puede superar los {ReglasBeneficiario.LongitudMaximaDireccion} caracteres.");

            return valor;
        }

        // Devuelve el número de identidad y la especificación del documento ya
        // resueltos según el tipo. Con "Sin documento" el número queda en null: no
        // se deja un número huérfano que no se corresponda con lo que muestra la pantalla.
        private static (string? NumIdentidad, string? TipoDocumentoOtro) ValidarDocumento(
            string? tipoDocumento, string? numIdentidad, string? tipoDocumentoOtro)
        {
            if (string.IsNullOrWhiteSpace(tipoDocumento))
                throw new ValidationException("El tipo de documento es obligatorio.");

            if (!TiposDocumento.EsValido(tipoDocumento))
                throw new ValidationException("El tipo de documento seleccionado no es válido.");

            var especificacion = ValidarNombreDelDocumento(tipoDocumento, tipoDocumentoOtro);

            if (!TiposDocumento.RequiereNumIdentidad(tipoDocumento))
                return (null, especificacion);

            // Se normaliza antes de validar: los espacios que agrupan el número no
            // son un error del usuario, se sacan. Es la forma en que se guarda, y la
            // que compara el índice único de documento.
            var numero = TextoNormalizador.NormalizarNumeroDocumento(numIdentidad);

            if (numero.Length == 0)
                throw new ValidationException("El número de identidad es obligatorio.");

            if (numero.Length > ReglasBeneficiario.LongitudMaximaNumIdentidad)
                throw new ValidationException(
                    $"El número de identidad no puede superar los {ReglasBeneficiario.LongitudMaximaNumIdentidad} caracteres.");

            // Mismo predicado y mismo texto que usa la pantalla para el MaxLength y
            // la ayuda del campo: si divergieran, el usuario vería una cosa y el
            // servidor exigiría otra.
            if (!ReglasBeneficiario.TieneFormatoNumIdentidad(tipoDocumento, numero))
                throw new ValidationException(
                    "El número de identidad no corresponde al tipo de documento. " +
                    $"Se espera: {ReglasBeneficiario.DescribirNumIdentidad(tipoDocumento)}");

            return (numero, especificacion);
        }

        private static string? ValidarNombreDelDocumento(string tipoDocumento, string? tipoDocumentoOtro)
        {
            // Solo se conserva la especificación cuando el tipo es "Otro".
            if (!TiposDocumento.RequiereNombreDelDocumento(tipoDocumento))
                return null;

            var valor = TextoNormalizador.CompactarEspacios(tipoDocumentoOtro);

            if (valor.Length == 0)
                throw new ValidationException("Debe indicar cómo se llama el documento cuando selecciona 'Otro'.");

            if (valor.Length > ReglasBeneficiario.LongitudMaximaTipoDocumentoOtro)
                throw new ValidationException(
                    $"El nombre del documento no puede superar los {ReglasBeneficiario.LongitudMaximaTipoDocumentoOtro} caracteres.");

            return valor;
        }
    }
}
