namespace SIGAC.Domain
{
    // Tipos de salida válidos: lista cerrada, respaldada por
    // CK_SalidasInventario_TipoSalida en la base. El servicio los asigna él mismo
    // (no llegan como texto libre desde un formulario), pero centralizarlos evita
    // que "Donacion" y "Prestamo" queden repetidos como literales sueltos.
    public static class TiposSalidaInventario
    {
        public const string Donacion = "Donacion";
        public const string Prestamo = "Prestamo";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Donacion,
            Prestamo
        };
    }
}
