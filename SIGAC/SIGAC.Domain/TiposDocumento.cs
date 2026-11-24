namespace SIGAC.Domain
{
    // Tipos de documento válidos: lista cerrada. Fuente única para el validador,
    // los filtros y la UI, igual que CategoriasBeneficiario.
    public static class TiposDocumento
    {
        public const string CedulaNacional = "Cédula nacional";

        // Solo para donantes y arrendatarios, que pueden ser empresas. Un
        // beneficiario es siempre una persona, así que NO está en Todos y el
        // validador de beneficiarios la rechaza.
        public const string CedulaJuridica = "Cédula jurídica";

        // DIMEX y "cédula de residencia" son el mismo documento (nombre técnico y
        // nombre popular), por eso van en una sola opción y no en dos.
        public const string Dimex = "DIMEX (cédula de residencia)";

        public const string Pasaporte = "Pasaporte";
        public const string Otro = "Otro";
        public const string SinDocumento = "Sin documento";

        // Los tipos de un beneficiario.
        public static readonly IReadOnlyList<string> Todos = new[]
        {
            CedulaNacional,
            Dimex,
            Pasaporte,
            Otro,
            SinDocumento
        };

        // Los tipos de un donante o un arrendatario: los de Todos más la cédula
        // jurídica, para registrar empresas y asociaciones.
        public static readonly IReadOnlyList<string> TodosParaOrganizaciones = new[]
        {
            CedulaNacional,
            CedulaJuridica,
            Dimex,
            Pasaporte,
            Otro,
            SinDocumento
        };

        // Validez para un beneficiario. Para donantes y arrendatarios, ver
        // EsValidoParaOrganizacion.
        public static bool EsValido(string? tipo) =>
            tipo is not null && Todos.Contains(tipo);

        public static bool EsValidoParaOrganizacion(string? tipo) =>
            tipo is not null && TodosParaOrganizaciones.Contains(tipo);

        // "Sin documento" es la única opción que no pide número de identidad: la UI
        // oculta el campo y el número guardado se borra. Aplica a todos los tipos
        // conocidos, también a la cédula jurídica.
        public static bool RequiereNumIdentidad(string? tipo) =>
            EsValidoParaOrganizacion(tipo) && tipo != SinDocumento;

        // "Otro" pide además cómo se llama el documento.
        public static bool RequiereNombreDelDocumento(string? tipo) =>
            tipo == Otro;

        // Tipo de persona (TiposPersonaDonante) que implica el documento, para que el
        // formulario de donantes y arrendatarios lo rellene solo al elegir el tipo.
        // La cédula nacional, el DIMEX y el pasaporte son documentos de personas; la
        // cédula jurídica es de una empresa o asociación. "Otro" y "Sin documento" no
        // dicen nada (puede ser una empresa extranjera): devuelve null y la persona
        // elige. Es una sugerencia: el campo sigue siendo editable.
        public static string? TipoPersonaSugerido(string? tipo) => tipo switch
        {
            CedulaNacional or Dimex or Pasaporte => TiposPersonaDonante.Fisica,
            CedulaJuridica => TiposPersonaDonante.Juridica,
            _ => null
        };

        // Tipos que se pueden consultar en Hacienda: la cédula nacional (9 dígitos) y
        // la jurídica (10). DIMEX, pasaporte y otros no están en ese servicio.
        public static bool SeConsultaEnHacienda(string? tipo) =>
            tipo is CedulaNacional or CedulaJuridica;
    }
}
