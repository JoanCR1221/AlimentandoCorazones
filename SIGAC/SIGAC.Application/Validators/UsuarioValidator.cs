using SIGAC.Application.DTOs.Seguridad;
using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Datos de un usuario nuevo ya validados y normalizados. El servicio solo los
    // copia: el correo va en minúsculas y sin espacios, el nombre compactado y
    // con mayúscula inicial (mismo criterio que BeneficiarioValidator).
    public sealed record UsuarioValidado(string Nombre, string Correo, string Password);

    public static class UsuarioValidator
    {
        public static UsuarioValidado Validar(UsuarioCrearDto dto) =>
            new(
                ValidarNombre(dto.Nombre),
                ValidarCorreo(dto.Correo),
                ValidarPasswordNueva(dto.Password, dto.ConfirmarPassword));

        public static string ValidarNombre(string? valor)
        {
            var normalizado = TextoNormalizador.NormalizarNombre(valor);

            if (normalizado.Length == 0)
                throw new ValidationException("El nombre es obligatorio.");

            if (normalizado.Length < ReglasUsuario.LongitudMinimaNombre)
                throw new ValidationException($"El nombre debe tener al menos {ReglasUsuario.LongitudMinimaNombre} caracteres.");

            if (normalizado.Length > ReglasUsuario.LongitudMaximaNombre)
                throw new ValidationException($"El nombre no puede superar los {ReglasUsuario.LongitudMaximaNombre} caracteres.");

            return normalizado;
        }

        // En minúsculas: Identity compara por la forma normalizada, pero guardar
        // siempre igual evita que el listado muestre "Juan@X.com" y "juan@x.com"
        // como si fueran dos formas distintas.
        public static string ValidarCorreo(string? valor)
        {
            var correo = (valor ?? string.Empty).Trim().ToLowerInvariant();

            if (correo.Length == 0)
                throw new ValidationException("El correo es obligatorio.");

            if (correo.Length > ReglasUsuario.LongitudMaximaCorreo)
                throw new ValidationException($"El correo no puede superar los {ReglasUsuario.LongitudMaximaCorreo} caracteres.");

            if (!ReglasUsuario.TieneFormatoCorreo(correo))
                throw new ValidationException("Escribí un correo válido, por ejemplo nombre@ejemplo.com.");

            return correo;
        }

        // Comprueba las reglas antes que Identity para que el mensaje sea uno solo
        // y en español, y para atrapar la confirmación distinta, que Identity no
        // conoce.
        public static string ValidarPasswordNueva(string? password, string? confirmacion)
        {
            if (string.IsNullOrEmpty(password))
                throw new ValidationException("La contraseña es obligatoria.");

            if (!ReglasUsuario.PasswordCumpleReglas(password))
                throw new ValidationException("La contraseña no cumple las reglas. " + ReglasUsuario.DescripcionReglasPassword);

            if (!string.Equals(password, confirmacion, StringComparison.Ordinal))
                throw new ValidationException("Las contraseñas no coinciden.");

            return password;
        }
    }
}
