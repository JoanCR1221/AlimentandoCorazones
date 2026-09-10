namespace SIGAC.Domain
{
    // Categorías de gasto operativo válidas: lista cerrada, mismo criterio que
    // CategoriasArticulo. La entidad todavía no tiene un CHECK que la respalde en
    // la base (ver GastoOperativo.cs); por ahora la única barrera es esta lista.
    public static class CategoriasGastoOperativo
    {
        public const string ServiciosBasicos = "ServiciosBasicos";
        public const string Transporte = "Transporte";
        public const string CompraInsumos = "CompraInsumos";
        public const string Salarios = "Salarios";
        public const string Viaticos = "Viaticos";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            ServiciosBasicos,
            Transporte,
            CompraInsumos,
            Salarios,
            Viaticos
        };

        public static bool EsValido(string? categoria) =>
            categoria is not null && Todos.Contains(categoria);
    }
}
