namespace SIGAC.Domain
{
    // Categorías de gasto operativo válidas: lista cerrada, mismo criterio que
    // CategoriasArticulo. El CHECK CK_GastosOperativos_Categoria la respalda en la
    // base y se genera a partir de esta misma lista, así que agregar un valor acá
    // exige una migración que reescriba la restricción.
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
