namespace SIGAC.Domain
{
    // A quién se le entrega una donación: lista cerrada. Actúa como discriminador
    // de DonacionEntregada, que según este valor usa BeneficiarioId (persona ya
    // registrada en SIGAC) o ComunidadDestinataria (grupo externo que no se
    // registra como beneficiario).
    //
    // Sin tilde en los valores, igual que OrigenesEntradaInventario y
    // TiposSalidaInventario: estos dos no se muestran como etiqueta directa sino
    // que clasifican el registro.
    public static class TiposDestinatarioDonacion
    {
        public const string Beneficiario = "Beneficiario";
        public const string Comunidad = "Comunidad";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Beneficiario,
            Comunidad
        };

        public static bool EsValido(string? tipoDestinatario) =>
            tipoDestinatario is not null && Todos.Contains(tipoDestinatario);
    }
}
