using System.Globalization;
using System.Text;

namespace SIGAC.Domain
{
    // Normalización de texto para nombres: una forma canónica para guardar y otra,
    // más agresiva, para comparar sin que las tildes o el formato generen duplicados.
    public static class TextoNormalizador
    {
        // Forma en que se guardan los nombres: sin espacios en los extremos y sin
        // espacios internos repetidos. Así el índice único de la BD compara igual.
        public static string CompactarEspacios(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return string.Empty;

            return string.Join(' ', valor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        // Clave de comparación: compactada, sin tildes y en mayúsculas, para que
        // "josé  pérez" y "Jose Perez" se detecten como la misma persona.
        public static string ClaveComparacion(string? valor)
        {
            var compactado = CompactarEspacios(valor);
            if (compactado.Length == 0)
                return string.Empty;

            var descompuesto = compactado.Normalize(NormalizationForm.FormD);
            var sinTildes = new StringBuilder(descompuesto.Length);

            foreach (var caracter in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                    sinTildes.Append(caracter);
            }

            return sinTildes.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        public static bool SonEquivalentes(string? a, string? b) =>
            string.Equals(ClaveComparacion(a), ClaveComparacion(b), StringComparison.Ordinal);
    }
}
