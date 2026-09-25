using SIGAC.Application.DTOs;
using SIGAC.Domain;

namespace SIGAC.Components.Shared
{
    // Textos comunes de las tarjetas de resumen, para que todos los módulos
    // digan lo mismo de la misma manera.
    public static class TextoResumen
    {
        // Número con separador de miles según la cultura de la página.
        public static string Numero(int valor) => valor.ToString("N0");

        // "1 inactivo" / "3 inactivos".
        public static string Cantidad(int valor, string singular, string plural) =>
            $"{Numero(valor)} {(valor == 1 ? singular : plural)}";

        // Montos por moneda en una línea ("₡ 1,500.00 · $ 20.00"). No se suman
        // monedas distintas entre sí. Sin montos, un guion.
        public static string Montos(IReadOnlyList<MontoPorMonedaDto> montos) =>
            montos.Count == 0
                ? "—"
                : string.Join(" · ", montos.Select(m => $"{TiposMoneda.Simbolo(m.Moneda)} {m.Total:N2}"));

        // Compara el mes en curso con el anterior, en palabras.
        public static string VsMesAnterior(int actual, int anterior)
        {
            var diferencia = actual - anterior;

            return diferencia switch
            {
                > 0 => $"{Numero(diferencia)} más que el mes anterior",
                < 0 => $"{Numero(-diferencia)} menos que el mes anterior",
                _ => "Igual que el mes anterior"
            };
        }
    }
}
