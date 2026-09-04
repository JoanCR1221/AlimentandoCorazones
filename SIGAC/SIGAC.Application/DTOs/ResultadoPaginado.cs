namespace SIGAC.Application.DTOs
{
    // Una página de resultados más el total de registros que cumplen los filtros.
    // El total no es el de la página: lo necesita la grilla para saber cuántas
    // páginas hay sin tener que traerlas todas.
    public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Elementos, int TotalRegistros)
    {
        public static ResultadoPaginado<T> Vacio { get; } = new(Array.Empty<T>(), 0);
    }
}
