namespace SIGAC.Domain
{
    // Categorías válidas de un beneficiario. Ya no se eligen a mano: se derivan
    // de la fecha de nacimiento. Fuente única para el servicio, los filtros y la UI.
    public static class CategoriasBeneficiario
    {
        public const string Nino = "Niño";
        public const string Adolescente = "Adolescente";
        public const string Adulto = "Adulto";
        public const string AdultoMayor = "Adulto mayor";

        public static readonly IReadOnlyList<string> Todas = new[]
        {
            Nino,
            Adolescente,
            Adulto,
            AdultoMayor
        };

        // Edad cumplida: descuenta un año si todavía no llegó el cumpleaños de este año.
        public static int CalcularEdad(DateTime fechaNacimiento, DateTime? fechaReferencia = null)
        {
            var referencia = (fechaReferencia ?? DateTime.Today).Date;
            var nacimiento = fechaNacimiento.Date;

            var edad = referencia.Year - nacimiento.Year;

            if (nacimiento > referencia.AddYears(-edad))
                edad--;

            return edad < 0 ? 0 : edad;
        }

        public static string DerivarDesdeFechaNacimiento(DateTime fechaNacimiento, DateTime? fechaReferencia = null)
        {
            var edad = CalcularEdad(fechaNacimiento, fechaReferencia);

            return edad switch
            {
                <= 11 => Nino,
                <= 17 => Adolescente,
                <= 64 => Adulto,
                _ => AdultoMayor
            };
        }
    }
}
