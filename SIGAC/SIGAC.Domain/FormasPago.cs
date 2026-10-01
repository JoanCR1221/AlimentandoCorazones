namespace SIGAC.Domain
{
    // Formas de pago de un gasto operativo: lista cerrada, mismo criterio que
    // TiposMoneda. Fuente única para el validador, el CHECK
    // CK_GastosOperativos_FormaPago y el desplegable del formulario.
    //
    // El reporte de la contadora se titula "GASTOS DE CONTADO", lo que sugiere que
    // existe uno de crédito: con las dos formas en el catálogo, ese segundo
    // reporte no exige migración.
    public static class FormasPago
    {
        public const string Contado = "Contado";
        public const string Credito = "Crédito";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Contado,
            Credito
        };

        public static bool EsValido(string? formaPago) =>
            formaPago is not null && Todos.Contains(formaPago);
    }
}
