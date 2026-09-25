namespace SIGAC.Domain
{
    // Categorías válidas de un beneficiario. No se eligen a mano ni se guardan: se
    // derivan de la fecha de nacimiento cada vez que hacen falta, así que no pueden
    // quedar desactualizadas cuando la persona cumple años. Fuente única para el
    // servicio, los filtros y la UI.
    public static class CategoriasBeneficiario
    {
        public const string Nino = "Niño";
        public const string Adolescente = "Adolescente";
        public const string Adulto = "Adulto";
        public const string AdultoMayor = "Adulto mayor";

        // Edad cumplida mínima y máxima de cada categoría (null = sin tope). Es la
        // única definición de los cortes: la usan tanto la derivación de una fecha
        // como el rango de fechas con el que se filtra en SQL.
        private static readonly (string Categoria, int EdadMinima, int? EdadMaxima)[] Rangos =
        {
            (Nino, 0, 11),
            (Adolescente, 12, 17),
            (Adulto, 18, 64),
            (AdultoMayor, 65, null)
        };

        public static readonly IReadOnlyList<string> Todas = Rangos.Select(r => r.Categoria).ToArray();

        public static bool EsValida(string? categoria) =>
            categoria is not null && Todas.Contains(categoria);

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

            return Rangos.First(r => edad >= r.EdadMinima && (r.EdadMaxima is null || edad <= r.EdadMaxima)).Categoria;
        }

        // Fechas de nacimiento que corresponden a una categoría en la fecha de
        // referencia, para filtrar en SQL sin guardar la categoría:
        //   NacidoDespuesDe < FechaNacimiento <= NacidoHasta   (null = sin límite)
        //
        // Misma definición de edad que CalcularEdad: tener N años cumplidos equivale
        // a haber nacido en referencia.AddYears(-N) o antes (con el 29 de febrero
        // incluido, porque AddYears lo lleva al 28 igual que CalcularEdad).
        public static (DateTime? NacidoDespuesDe, DateTime? NacidoHasta) RangoDeNacimiento(
            string categoria, DateTime? fechaReferencia = null)
        {
            var referencia = (fechaReferencia ?? DateTime.Today).Date;
            var rango = Rangos.First(r => r.Categoria == categoria);

            // Edad mínima N: nacido a más tardar hace N años. Con N = 0 no hay tope,
            // así también entra una fecha futura que no debería existir.
            DateTime? hasta = rango.EdadMinima == 0 ? null : referencia.AddYears(-rango.EdadMinima);

            // Edad máxima M: todavía no cumplió M + 1, o sea nacido después de esa fecha.
            DateTime? despuesDe = rango.EdadMaxima is int maxima ? referencia.AddYears(-(maxima + 1)) : null;

            return (despuesDe, hasta);
        }
    }
}
