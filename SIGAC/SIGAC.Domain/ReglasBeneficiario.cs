using System.Text.RegularExpressions;

namespace SIGAC.Domain
{
    // Límites y formato de los datos personales de un beneficiario. Las longitudes
    // se comparten con el mapeo de EF Core para que no se desincronicen.
    public static class ReglasBeneficiario
    {
        public const int LongitudMinimaNombre = 2;
        public const int LongitudMaximaNombre = 100;
        public const int LongitudMaximaApellido = 75;
        public const int EdadMaximaAnios = 120;

        // Documentos de identidad. La cédula nacional y el DIMEX son puramente
        // numéricos y de longitud fija; el pasaporte y "Otro" son alfanuméricos.
        public const int DigitosCedulaNacional = 9;
        public const int DigitosMinimosDimex = 11;
        public const int DigitosMaximosDimex = 12;
        public const int LongitudMaximaNumIdentidad = 30;
        public const int LongitudMaximaTipoDocumentoOtro = 100;

        // Teléfono de Costa Rica: 8 dígitos, sin guiones ni espacios. Uno solo por
        // beneficiario, por eso es una columna simple y no una lista.
        public const int DigitosTelefono = 8;

        public const int LongitudMaximaDireccion = 200;

        // Letras (con tildes y ñ), apóstrofes, guiones y un espacio entre partes:
        // aparecen en apellidos reales (D'Ávila, Sánchez-Mora, De la Cruz).
        // Se rechazan dígitos y cualquier otro símbolo.
        private static readonly Regex FormatoNombre = new(
            @"^[\p{L}\p{M}]+(?:[ '’\-][\p{L}\p{M}]+)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Solo dígitos ASCII: nada de guiones, espacios ni separadores.
        private static readonly Regex SoloDigitos = new(
            @"^[0-9]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Alfanumérico estricto: números y letras sin tildes, sin espacios ni signos.
        private static readonly Regex Alfanumerico = new(
            @"^[A-Za-z0-9]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Espera un valor ya compactado con TextoNormalizador.CompactarEspacios.
        public static bool TieneFormatoValido(string? valor) =>
            !string.IsNullOrWhiteSpace(valor) && FormatoNombre.IsMatch(valor);

        public static bool EsAlfanumerico(string? valor) =>
            !string.IsNullOrWhiteSpace(valor) && Alfanumerico.IsMatch(valor);

        private static bool TieneSoloDigitos(string? valor) =>
            !string.IsNullOrWhiteSpace(valor) && SoloDigitos.IsMatch(valor);

        public static bool TieneFormatoCedulaNacional(string? valor) =>
            TieneSoloDigitos(valor) && valor!.Length == DigitosCedulaNacional;

        public static bool TieneFormatoDimex(string? valor) =>
            TieneSoloDigitos(valor)
            && valor!.Length >= DigitosMinimosDimex
            && valor.Length <= DigitosMaximosDimex;

        public static bool TieneFormatoTelefono(string? valor) =>
            TieneSoloDigitos(valor) && valor!.Length == DigitosTelefono;

        public static string ComponerNombreCompleto(
            string? primerNombre, string? segundoNombre, string? primerApellido, string? segundoApellido) =>
            string.Join(' ', new[] { primerNombre, segundoNombre, primerApellido, segundoApellido }
                .Where(parte => !string.IsNullOrWhiteSpace(parte))
                .Select(parte => parte!.Trim()));
    }
}
