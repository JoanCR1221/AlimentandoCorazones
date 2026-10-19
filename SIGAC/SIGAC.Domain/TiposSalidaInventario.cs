namespace SIGAC.Domain
{
    // Tipos de salida válidos: lista cerrada, respaldada por
    // CK_SalidasInventario_TipoSalida en la base. El servicio los asigna él mismo
    // (no llegan como texto libre desde un formulario), pero centralizarlos evita
    // que "Donacion" quede repetido como literal suelto.
    //
    // Hoy hay un solo tipo: el préstamo de artículos se eliminó del sistema
    // (migración EliminaPrestamosDeArticulos). Se conserva el catálogo para que
    // un tipo de salida nuevo se agregue acá y en el CHECK, sin tocar la columna.
    public static class TiposSalidaInventario
    {
        public const string Donacion = "Donacion";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Donacion
        };
    }
}
