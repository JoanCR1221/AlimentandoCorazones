namespace SIGAC.Domain
{
    // Categorías de artículo válidas: lista cerrada, igual que OrigenesEntradaInventario
    // y TiposSalidaInventario. Fuente única para el servicio y para los desplegables
    // de las pantallas: antes las cuatro categorías estaban repetidas como literales
    // en tres archivos .razor, y nada impedía guardar una categoría inventada desde
    // fuera del formulario.
    //
    // A diferencia de Origen y TipoSalida, esta lista NO tiene un CHECK que la
    // respalde en la base: la columna Categoria es un varchar(100) libre. La
    // validación del servicio es hoy la única barrera.
    public static class CategoriasArticulo
    {
        public const string Alimento = "Alimento";
        public const string Ropa = "Ropa";
        public const string Calzado = "Calzado";
        public const string Equipo = "Equipo";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Alimento,
            Ropa,
            Calzado,
            Equipo
        };

        public static bool EsValido(string? categoria) =>
            categoria is not null && Todos.Contains(categoria);
    }
}
