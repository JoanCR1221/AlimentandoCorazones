namespace SIGAC.Domain
{
    // Orígenes de entrada válidos: lista cerrada, respaldada por
    // CK_EntradasInventario_Origen en la base. Fuente única para el servicio y la
    // UI, igual que TiposDocumento y TiemposComida: antes de esto "Donacion" y
    // "Compra" eran literales sueltos en InventarioService, y una entrada con un
    // origen mal escrito solo se detectaba cuando la base rechazaba el INSERT.
    public static class OrigenesEntradaInventario
    {
        public const string Donacion = "Donacion";
        public const string Compra = "Compra";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Donacion,
            Compra
        };

        public static bool EsValido(string? origen) =>
            origen is not null && Todos.Contains(origen);
    }
}
