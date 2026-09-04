namespace SIGAC.Domain
{
    // Unidades de medida: lista abierta, mismo criterio que CategoriasArticulo.
    // El usuario puede escribir una unidad nueva y queda guardada para la próxima
    // vez. Estas cinco son solo el punto de partida para un catálogo vacío.
    public static class UnidadesMedidaArticulo
    {
        public const int LongitudMaxima = 50;

        public const string Unidad = "Unidad";
        public const string Kilogramo = "Kilogramo";
        public const string Litro = "Litro";
        public const string Caja = "Caja";
        public const string Paquete = "Paquete";

        public static readonly IReadOnlyList<string> Sugeridas = new[]
        {
            Unidad,
            Kilogramo,
            Litro,
            Caja,
            Paquete
        };
    }
}
