namespace SIGAC.Domain
{
    // Tipos de persona de un donante: lista cerrada, igual que TiposDocumento y
    // CategoriasArticulo. Fuente única para el servicio, los filtros y los
    // desplegables de las pantallas de Donaciones.
    //
    // El valor guardado lleva tilde ("Física", "Jurídica") porque es el texto que
    // se muestra tal cual en la UI, mismo criterio que TiposDocumento
    // ("Cédula nacional") y CategoriasBeneficiario ("Niño"). Los nombres de las
    // constantes van sin tilde por convención de C#.
    public static class TiposPersonaDonante
    {
        public const string Fisica = "Física";
        public const string Juridica = "Jurídica";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Fisica,
            Juridica
        };

        public static bool EsValido(string? tipoPersona) =>
            tipoPersona is not null && Todos.Contains(tipoPersona);
    }
}
