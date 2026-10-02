namespace SIGAC.Domain.Entities
{
    // Horas en que se puede alquilar el local, configurables por la asociación
    // desde "Espacios y características". Es una tabla de UNA sola fila (Id = 1,
    // respaldado por CK_HorarioAlquiler_FilaUnica): no hay varios horarios que
    // elegir, hay uno vigente que se edita.
    //
    // Cambiarlo solo afecta a los alquileres que se registren después: los ya
    // reservados no se revalidan.
    public class HorarioAlquiler
    {
        public const int IdUnico = 1;

        public int Id { get; set; } = IdUnico;

        // Lunes a viernes.
        public TimeSpan AperturaEntreSemana { get; set; }
        public TimeSpan CierreEntreSemana { get; set; }

        // Sábado y domingo.
        public TimeSpan AperturaFinDeSemana { get; set; }
        public TimeSpan CierreFinDeSemana { get; set; }

        // Horario con el que arranca el sistema (lo siembra la migración) y el que
        // se usa si la fila llegara a faltar: L-V de 8:00 a 20:00, S-D de 8:00 a
        // 17:00, lo que definió la asociación al crear el módulo.
        public static HorarioAlquiler PorDefecto() => new()
        {
            Id = IdUnico,
            AperturaEntreSemana = new TimeSpan(8, 0, 0),
            CierreEntreSemana = new TimeSpan(20, 0, 0),
            AperturaFinDeSemana = new TimeSpan(8, 0, 0),
            CierreFinDeSemana = new TimeSpan(17, 0, 0)
        };
    }
}
