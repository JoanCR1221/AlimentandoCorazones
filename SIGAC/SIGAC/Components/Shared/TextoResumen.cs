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

        // Horas con minutos: 12,5 → "12 h 30 min", 3 → "3 h", 0,25 → "15 min". Sin
        // decimales porque "12,5 horas" se lee peor que "12 h 30 min"; las horas con
        // separador de miles ("9 673 h") como el resto de las cifras.
        public static string Horas(decimal horas)
        {
            var minutos = (int)Math.Round(horas * 60m);
            var h = minutos / 60;
            var m = minutos % 60;

            return (h, m) switch
            {
                (_, 0) => $"{Numero(h)} h",
                (0, _) => $"{m} min",
                _ => $"{Numero(h)} h {m:00} min"
            };
        }

        // Período de un reporte en palabras, para el subtítulo del PDF y el Excel:
        // "01/09/2026 al 30/09/2026", "desde el ...", "hasta el ..." o, sin acotar,
        // "todo el historial".
        public static string Periodo(DateTime? desde, DateTime? hasta) => (desde, hasta) switch
        {
            ({ } d, { } h) => $"{d:dd/MM/yyyy} al {h:dd/MM/yyyy}",
            ({ } d, null) => $"desde el {d:dd/MM/yyyy}",
            (null, { } h) => $"hasta el {h:dd/MM/yyyy}",
            _ => "todo el historial"
        };

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
