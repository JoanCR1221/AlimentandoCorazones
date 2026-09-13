namespace SIGAC.Application.DTOs
{
    // Un total junto con la moneda en la que está expresado. Reemplaza a un
    // decimal suelto en los resultados que pueden mezclar más de una moneda
    // (donaciones en dinero, gastos operativos): sumar montos de monedas
    // distintas en un solo número no representa nada, así que el total se
    // separa uno por cada moneda presente en el período consultado.
    public sealed record MontoPorMonedaDto(string Moneda, decimal Total);
}
