using SIGAC.Domain.Entities;

namespace SIGAC.Domain
{
    // Horarios permitidos y reglas de choque de los alquileres de espacios. Los
    // comparten la pantalla de registro (para avisar antes de guardar) y
    // AlquilerValidator/AlquileresService (que son los que mandan), así que no
    // pueden discrepar. Mismo criterio que ReglasAsistencia.
    //
    // Reemplaza a los campos EsFinDeSemana y HorarioFinDeSemana de la propuesta
    // original: el fin de semana se deduce de la fecha y el horario es una regla de
    // la asociación, no un dato que se cargue en cada alquiler. Las horas en sí no
    // viven acá: son configurables (HorarioAlquiler, una fila en la base) y llegan
    // como parámetro.
    public static class ReglasAlquiler
    {
        // Un alquiler de menos tiempo que esto es casi seguro un error de carga
        // (inicio y fin iguales, o un minuto de diferencia). También es lo mínimo
        // que tiene que durar la franja de un horario configurado.
        public const int DuracionMinimaMinutos = 30;

        public static bool EsFinDeSemana(DateTime fecha) =>
            fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        public static (TimeSpan Apertura, TimeSpan Cierre) HorarioPermitido(DateTime fecha, HorarioAlquiler horario) =>
            EsFinDeSemana(fecha)
                ? (horario.AperturaFinDeSemana, horario.CierreFinDeSemana)
                : (horario.AperturaEntreSemana, horario.CierreEntreSemana);

        // Texto de ayuda para la pantalla y para el mensaje de error del validador:
        // una sola redacción para las dos cosas.
        public static string DescribirHorario(DateTime fecha, HorarioAlquiler horario)
        {
            var (apertura, cierre) = HorarioPermitido(fecha, horario);
            var dias = EsFinDeSemana(fecha) ? "Sábado y domingo" : "De lunes a viernes";

            // Sin punto final: la hora ya termina en "a. m." o "p. m.".
            return $"{dias} se alquila de {FormatearHora(apertura)} a {FormatearHora(cierre)}";
        }

        // Formato de 12 horas con a. m./p. m., el mismo que muestran los selectores
        // de hora: "8:00 a. m.", "12:00 p. m." (mediodía), "5:30 p. m.". Se arma a
        // mano y no con la cultura del servidor para que el calendario, los mensajes
        // y la bitácora digan lo mismo en cualquier equipo. Solo es presentación:
        // en la base las horas siguen siendo "time" de 24 horas.
        public static string FormatearHora(TimeSpan hora)
        {
            var horas12 = hora.Hours % 12 == 0 ? 12 : hora.Hours % 12;
            var sufijo = hora.Hours < 12 ? "a. m." : "p. m.";

            return $"{horas12}:{hora.Minutes:00} {sufijo}";
        }

        public static string FormatearFranja(TimeSpan inicio, TimeSpan fin) =>
            $"{FormatearHora(inicio)} – {FormatearHora(fin)}";

        public static bool EstaDentroDelHorario(DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin, HorarioAlquiler horario)
        {
            var (apertura, cierre) = HorarioPermitido(fecha, horario);
            return horaInicio >= apertura && horaFin <= cierre;
        }

        // Dos franjas del mismo día chocan si se superponen en algún momento. Que una
        // termine justo cuando empieza la otra (10:00-12:00 y 12:00-14:00) NO es
        // choque: son alquileres seguidos.
        public static bool SeTraslapan(TimeSpan inicioA, TimeSpan finA, TimeSpan inicioB, TimeSpan finB) =>
            inicioA < finB && inicioB < finA;

        // No se reserva para días que ya pasaron. Hoy sí se admite: puede alquilarse
        // el local para esa misma tarde.
        public static bool EsFechaValidaParaReservar(DateTime fecha, DateTime? hoy = null) =>
            fecha.Date >= (hoy ?? DateTime.Today).Date;
    }
}
