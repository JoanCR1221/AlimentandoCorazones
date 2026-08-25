namespace SIGAC.Domain
{
    // Límites del registro de asistencia al comedor. Los comparten la pantalla de
    // registro y AsistenciaService, para que el calendario y la validación del
    // servidor no puedan discrepar.
    //
    // Aplican solo al REGISTRO. El historial consulta fechas viejas sin restricción.
    public static class ReglasAsistencia
    {
        // La asistencia se registra el día en que ocurre. Se aceptan días anteriores
        // solo para cubrir olvidos, hasta este tope.
        public const int MaximoDiasHaciaAtras = 3;

        // Un beneficiario no puede registrarse más veces por día que tiempos de
        // comida hay (el índice único ya impide repetir el mismo tiempo de comida).
        public const int MaximoRegistrosPorDia = 3;

        // "Hoy" para todas las reglas de asistencia: una sola fuente para la pantalla
        // y el servicio, y siempre sin hora. Comparar con hora haría que una fecha
        // elegida al mediodía se viera como futura frente a una medianoche.
        public static DateTime Hoy => DateTime.Today;

        // Fecha más antigua que se puede registrar.
        public static DateTime FechaMinimaRegistro(DateTime? fechaReferencia = null) =>
            (fechaReferencia ?? Hoy).Date.AddDays(-MaximoDiasHaciaAtras);

        // Ventana válida de registro: desde FechaMinimaRegistro hasta hoy inclusive.
        public static bool FechaRegistroEnRango(DateTime fecha, DateTime? fechaReferencia = null)
        {
            var hoy = (fechaReferencia ?? Hoy).Date;
            var dia = fecha.Date;

            return dia >= FechaMinimaRegistro(hoy) && dia <= hoy;
        }
    }
}
