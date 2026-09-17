namespace SIGAC.Domain
{
    // Límites y formato de los datos de un usuario del sistema. Los comparten el
    // validador de Application, la configuración de Identity en Program.cs y las
    // ayudas de las pantallas, para que la regla escrita en el formulario sea la
    // misma que rechaza el servidor.
    public static class ReglasUsuario
    {
        public const int LongitudMinimaNombre = 2;
        public const int LongitudMaximaNombre = 150;

        // Tope de Identity para Email y UserName (nvarchar(256)).
        public const int LongitudMaximaCorreo = 256;

        // Contraseña (AB#1215): 8 caracteres con mayúscula, minúscula, número y
        // símbolo. Identity aplica lo mismo desde IdentityOptions; acá vive para
        // que el validador dé el mensaje en español antes de llegar a Identity y
        // para que la pantalla lo muestre como ayuda.
        public const int LongitudMinimaPassword = 8;

        // Bloqueo temporal por intentos fallidos.
        public const int MaximoIntentosFallidos = 5;
        public const int MinutosBloqueo = 15;

        public static string DescripcionReglasPassword =>
            $"Mínimo {LongitudMinimaPassword} caracteres, con al menos una mayúscula, una minúscula, un número y un símbolo.";

        public static bool PasswordCumpleReglas(string? password) =>
            !string.IsNullOrEmpty(password)
            && password.Length >= LongitudMinimaPassword
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(c => !char.IsLetterOrDigit(c));

        // Formato mínimo razonable: algo@algo.algo, sin espacios. No se pretende
        // validar contra el RFC completo; Identity vuelve a comprobarlo.
        public static bool TieneFormatoCorreo(string? correo)
        {
            if (string.IsNullOrWhiteSpace(correo) || correo.Any(char.IsWhiteSpace))
                return false;

            var arroba = correo.IndexOf('@');
            if (arroba <= 0 || arroba != correo.LastIndexOf('@') || arroba == correo.Length - 1)
                return false;

            var dominio = correo[(arroba + 1)..];
            var punto = dominio.IndexOf('.');

            return punto > 0 && punto < dominio.Length - 1;
        }
    }
}
