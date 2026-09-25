namespace SIGAC.Domain
{
    // Estados válidos de un artículo de categoría Equipo: lista cerrada, igual que
    // CategoriasArticulo. Subcategoriza el Equipo (una silla nueva y una dañada no
    // son lo mismo en la bodega), y por eso el estado forma parte de la identidad del
    // artículo: "Silla / Nuevo" y "Silla / Dañado" son dos filas del catálogo, cada
    // una con su propio stock (ver UX_Articulos_Nombre_Estado en SigacDbContext).
    //
    // Solo aplica a Equipo. Alimento, Ropa y Calzado se consumen o se entregan de
    // forma definitiva y no tienen estado: su columna Estado queda en NULL.
    public static class EstadosArticulo
    {
        public const string Nuevo = "Nuevo";
        public const string EnBuenEstado = "En buen estado";
        public const string Danado = "Dañado";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Nuevo,
            EnBuenEstado,
            Danado
        };

        public static bool EsValido(string? estado) =>
            estado is not null && Todos.Contains(estado);

        // La única categoría con estado. Vive acá y no repetida en cada pantalla y
        // validador para que "qué categorías llevan estado" tenga una sola respuesta.
        public static bool AplicaACategoria(string? categoria) =>
            categoria == CategoriasArticulo.Equipo;
    }
}
