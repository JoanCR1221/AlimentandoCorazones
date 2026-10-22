using System.Globalization;
using MudBlazor;

namespace SIGAC.Components.Shared
{
    // Texto alternativo de las gráficas de los panoramas: MudChart dibuja un SVG sin
    // descripción, así que un lector de pantalla no anuncia nada. Se resume el título
    // y las cifras ("Altas por mes. Series Altas: Ene 2026: 3, Feb 2026: 5") para que
    // quien no ve el gráfico tenga los mismos datos.
    public static class TextoAlternativoGrafica
    {
        public static string De(string titulo, IReadOnlyList<string> etiquetas, IEnumerable<ChartSeries<double>> series)
        {
            var partes = new List<string> { titulo };

            foreach (var serie in series)
            {
                var datos = serie.Data ?? Array.Empty<double>();
                var puntos = datos.Select((valor, i) =>
                    $"{(i < etiquetas.Count ? etiquetas[i] : (i + 1).ToString(CultureInfo.CurrentCulture))}: {Formatear(valor)}");

                partes.Add($"{serie.Name}: {string.Join(", ", puntos)}");
            }

            return string.Join(". ", partes);
        }

        // Enteros sin decimales (conteos) y el resto con dos (montos).
        private static string Formatear(double valor) =>
            valor == Math.Truncate(valor) ? valor.ToString("N0") : valor.ToString("N2");
    }
}
