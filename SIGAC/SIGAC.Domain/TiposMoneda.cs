namespace SIGAC.Domain
{
    // Monedas en las que se puede recibir una donación en dinero o pagar un gasto
    // operativo: lista cerrada, mismo criterio que TiposPersonaDonante y
    // FormasPago. Fuente única para el servicio, los CHECK de la
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

        // Posición de la moneda en Todos (colones, dólares, euros), para listar los
        // totales por moneda siempre en el mismo orden y no en el que devuelva la base.
        // Una que no esté en la lista (dato viejo) va al final, no se pierde.
        public static int Posicion(string? moneda)
        {
            for (var i = 0; i < Todos.Count; i++)
            {
                if (Todos[i] == moneda)
                    return i;
            }

            return int.MaxValue;
        }

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

        // Monto listo para mostrar: "₡ 164 600,00". El espacio entre símbolo y
        // número es no separable (U+00A0): con uno común, en una celda o un título
        // angosto el navegador partía la línea ahí y dejaba el "₡" solo arriba.
        public static string ConSimbolo(string? moneda, decimal monto) =>
            $"{Simbolo(moneda)} {monto:N2}";
    }
}
