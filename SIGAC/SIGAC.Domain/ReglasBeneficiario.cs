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

        // Letras (con tildes y ñ), apóstrofes, guiones y un espacio entre partes:
        // aparecen en apellidos reales (D'Ávila, Sánchez-Mora, De la Cruz).
        // Se rechazan dígitos y cualquier otro símbolo.
        private static readonly Regex FormatoNombre = new(
            @"^[\p{L}\p{M}]+(?:[ '’\-][\p{L}\p{M}]+)*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Espera un valor ya compactado con TextoNormalizador.CompactarEspacios.
        public static bool TieneFormatoValido(string? valor) =>
            !string.IsNullOrWhiteSpace(valor) && FormatoNombre.IsMatch(valor);

        public static string ComponerNombreCompleto(string? nombre, string? primerApellido, string? segundoApellido) =>
            string.Join(' ', new[] { nombre, primerApellido, segundoApellido }
                .Where(parte => !string.IsNullOrWhiteSpace(parte))
                .Select(parte => parte!.Trim()));
    }
}
