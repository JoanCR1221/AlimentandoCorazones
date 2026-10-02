using System.Text;
using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Datos de un arrendatario ya validados y normalizados, listos para persistir.
    // El servicio solo los copia a la entidad: no vuelve a limpiar ni a decidir nada.
    public sealed record ArrendatarioValidado(
        string Nombre,
        string TipoPersona,
        string? Identificacion,
        string CodigoPaisTelefono,
        string Telefono,
        string? Correo);

    // Toda la validación y normalización de entrada del arrendatario. Reutiliza de
    // DonanteValidator lo que es idéntico (tipo de persona y correo) para que los dos
    // módulos no exijan cosas distintas del mismo dato.
    //
    // Los largos máximos coinciden con las columnas de Arrendatarios en
    // SigacDbContext: si cambia una columna, hay que mover también la constante.
    public static class ArrendatarioValidator
    {
        public const int LongitudMaximaNombre = 150;
        public const int LongitudMaximaIdentificacion = 30;
        public const int LongitudMaximaCorreo = 150;

        public static ArrendatarioValidado Validar(ArrendatarioCrearDto dto)
        {
            var nombre = ValidarNombre(dto.Nombre);
            var tipoPersona = DonanteValidator.ValidarTipoPersona(dto.TipoPersona);
            var identificacion = NormalizarIdentificacion(dto.Identificacion);

            // Misma regla de formato que Beneficiario y Donante, pero obligatorio: sin
            // número, TelefonoValidator devuelve los dos campos en null.
            var telefono = TelefonoValidator.Validar(dto.CodigoPaisTelefono, dto.Telefono);

            if (telefono.Numero is null)
                throw new ValidationException("El teléfono del arrendatario es obligatorio.");

            return new ArrendatarioValidado(
                nombre,
                tipoPersona,
                identificacion,
                telefono.CodigoPais!,
                telefono.Numero,
                DonanteValidator.ValidarCorreo(dto.Correo));
        }

        // CompactarEspacios y no NormalizarNombre, mismo criterio que
        // DonanteValidator: el arrendatario puede ser una razón social.
        public static string ValidarNombre(string? valor)
        {
            var normalizado = TextoNormalizador.CompactarEspacios(valor);

            if (normalizado.Length == 0)
                throw new ValidationException("El nombre del arrendatario es obligatorio.");

            if (normalizado.Length > LongitudMaximaNombre)
                throw new ValidationException(
                    $"El nombre no puede superar los {LongitudMaximaNombre} caracteres.");

            return normalizado;
        }

        // Forma en que se guarda y se compara la identificación: sin espacios NI
        // guiones. Una cédula se escribe tanto "1-2345-6789" como "123456789", y sin
        // esto el índice único las trataría como dos personas distintas. Es más
        // agresivo que NormalizarNumeroDocumento de Beneficiario (que conserva el
        // guion) porque acá no hay tipo de documento que diga si el guion es parte
        // del número.
        //
        // Opcional: vacía se guarda como NULL y queda fuera del índice único.
        public static string? NormalizarIdentificacion(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return null;

            var normalizada = new StringBuilder(valor.Length);

            foreach (var caracter in valor)
            {
                if (char.IsWhiteSpace(caracter) || caracter == '-')
                    continue;

                if (!char.IsAsciiLetterOrDigit(caracter))
                    throw new ValidationException(
                        "La identificación solo admite números y letras (los espacios y guiones se ignoran).");

                normalizada.Append(char.ToUpperInvariant(caracter));
            }

            if (normalizada.Length == 0)
                return null;

            if (normalizada.Length > LongitudMaximaIdentificacion)
                throw new ValidationException(
                    $"La identificación no puede superar los {LongitudMaximaIdentificacion} caracteres.");

            return normalizada.ToString();
        }
    }
}
