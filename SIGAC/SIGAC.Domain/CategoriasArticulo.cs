namespace SIGAC.Domain
{
    // Categorías de artículo: lista abierta. A diferencia de TiposDocumento o
    // TiemposComida, acá el usuario puede escribir una categoría nueva y queda
    // guardada para la próxima vez (se sugiere junto con las que ya existen en el
    // catálogo, ver InventarioService.ObtenerCategoriasAsync). Estas cuatro son
    // solo el punto de partida para un catálogo vacío.
    public static class CategoriasArticulo
    {
        public const int LongitudMaxima = 100;

        public const string Alimentos = "Alimentos";
        public const string Ropa = "Ropa";
        public const string Calzado = "Calzado";
        public const string Equipos = "Equipos";

        public static readonly IReadOnlyList<string> Sugeridas = new[]
        {
            Alimentos,
            Ropa,
            Calzado,
            Equipos
        };
    }
}
