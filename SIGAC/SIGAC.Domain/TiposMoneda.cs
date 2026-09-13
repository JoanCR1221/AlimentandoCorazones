namespace SIGAC.Domain
{
    // Monedas en las que se puede recibir una donación en dinero o pagar un gasto
    // operativo: lista cerrada, mismo criterio que TiposPersonaDonante y
    // CategoriasGastoOperativo. Fuente única para el servicio, los CHECK de la
    // base y los desplegables de los formularios.
    public static class TiposMoneda
    {
        public const string Colones = "Colones";
        public const string Dolares = "Dólares";
        public const string Euros = "Euros";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Colones,
            Dolares,
            Euros
        };

        public static bool EsValido(string? moneda) =>
            moneda is not null && Todos.Contains(moneda);

        // Símbolo para mostrar junto al monto en formularios e historiales. No es
        // una columna de ninguna tabla: se deriva de Moneda cada vez que hace
        // falta mostrarlo, para que agregar una moneda no obligue a guardar
        // también su símbolo.
        public static string Simbolo(string? moneda) => moneda switch
        {
            Colones => "₡",
            Dolares => "$",
            Euros => "€",
            _ => string.Empty
        };
    }
}
