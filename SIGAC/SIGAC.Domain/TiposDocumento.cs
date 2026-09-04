namespace SIGAC.Domain
{
    // Tipos de documento válidos: lista cerrada. Fuente única para el validador,
    // los filtros y la UI, igual que CategoriasBeneficiario.
    public static class TiposDocumento
    {
        public const string CedulaNacional = "Cédula nacional";

        // DIMEX y "cédula de residencia" son el mismo documento (nombre técnico y
        // nombre popular), por eso van en una sola opción y no en dos.
        public const string Dimex = "DIMEX (cédula de residencia)";

        public const string Pasaporte = "Pasaporte";
        public const string Otro = "Otro";
        public const string SinDocumento = "Sin documento";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            CedulaNacional,
            Dimex,
            Pasaporte,
            Otro,
            SinDocumento
        };

        public static bool EsValido(string? tipo) =>
            tipo is not null && Todos.Contains(tipo);

        // "Sin documento" es la única opción que no pide número de identidad: la UI
        // oculta el campo y el número guardado se borra.
        public static bool RequiereNumIdentidad(string? tipo) =>
            EsValido(tipo) && tipo != SinDocumento;

        // "Otro" pide además cómo se llama el documento.
        public static bool RequiereNombreDelDocumento(string? tipo) =>
            tipo == Otro;
    }
}
