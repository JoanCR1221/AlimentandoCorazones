namespace SIGAC.Domain
{
    // Unidades de medida válidas y, sobre todo, QUÉ unidad tiene sentido para cada
    // categoría. Antes las cinco unidades se ofrecían siempre, sin importar la
    // categoría, así que se podía registrar "Calzado" medido en "Litro".
    //
    // La correspondencia vive acá y no en la pantalla para que el desplegable y la
    // validación del servicio no puedan divergir: la lista que ve el usuario y la
    // que se exige al guardar salen del mismo diccionario.
    public static class UnidadesMedidaArticulo
    {
        public const string Unidad = "Unidad";
        public const string Kilogramo = "Kilogramo";
        public const string Litro = "Litro";
        public const string Caja = "Caja";
        public const string Paquete = "Paquete";

        // Todas las unidades que existen, sin filtrar por categoría. Sirve para
        // listados y reportes; para un formulario se usa ObtenerUnidadesValidas.
        public static readonly IReadOnlyList<string> Todas = new[]
        {
            Unidad,
            Kilogramo,
            Litro,
            Caja,
            Paquete
        };

        // El orden de cada lista es el orden en que se muestran en el desplegable:
        // primero la unidad más habitual de esa categoría.
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> UnidadesPorCategoria =
            new Dictionary<string, IReadOnlyList<string>>
            {
                [CategoriasArticulo.Alimento] = new[] { Kilogramo, Litro, Caja, Paquete, Unidad },
                [CategoriasArticulo.Ropa] = new[] { Unidad, Paquete },
                [CategoriasArticulo.Calzado] = new[] { Unidad, Paquete },
                [CategoriasArticulo.Equipo] = new[] { Unidad, Caja }
            };

        // Devuelve lista vacía (y no todas las unidades) cuando la categoría es nula,
        // vacía o desconocida: el desplegable de la pantalla se arma con esto, y
        // ofrecer las cinco unidades ante una categoría sin elegir es justo el
        // comportamiento que se está corrigiendo.
        public static IReadOnlyList<string> ObtenerUnidadesValidas(string? categoria) =>
            categoria is not null && UnidadesPorCategoria.TryGetValue(categoria, out var unidades)
                ? unidades
                : Array.Empty<string>();

        public static bool EsValidaParaCategoria(string? categoria, string? unidadMedida) =>
            unidadMedida is not null && ObtenerUnidadesValidas(categoria).Contains(unidadMedida);
    }
}
