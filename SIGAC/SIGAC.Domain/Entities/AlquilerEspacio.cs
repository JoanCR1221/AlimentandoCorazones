namespace SIGAC.Domain.Entities
{
    public enum EstadoAlquiler
    {
        Reservado,
        Cancelado
    }

    // Reserva de uno o varios sectores del local por un arrendatario, en una fecha y
    // una franja horaria. Los sectores y las características pedidas viven en tablas
    // intermedias (AlquileresEspaciosFisicos y AlquileresCaracteristicas) y no en
    // columnas, porque un mismo alquiler usa varios de cada uno.
    //
    // Sin EsFinDeSemana ni HorarioFinDeSemana, que traía la propuesta: los dos salen
    // de Fecha (ReglasAlquiler.EsFinDeSemana / HorarioPermitido). Guardarlos aparte
    // permitiría un sábado marcado como "no fin de semana" y obligaría a mantenerlos
    // al día cada vez que se corrige la fecha.
    public class AlquilerEspacio
    {
        public int Id { get; set; }

        public int ArrendatarioId { get; set; }
        public Arrendatario? Arrendatario { get; set; }

        // Solo el día (columna date). La hora vive en HoraInicio/HoraFin.
        public DateTime Fecha { get; set; }

        // Columnas time. El alquiler no cruza la medianoche: HoraFin > HoraInicio lo
        // exige CK_AlquileresEspacio_Horas.
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }

        // Personas que se esperan. No estaba en la propuesta: sin él,
        // EspacioFisico.Capacidad no tenía contra qué compararse, y sirve para
        // preparar el mobiliario.
        public int CantidadPersonas { get; set; }

        // Monto total acordado. 0 es válido: un préstamo sin costo a un grupo de la
        // comunidad también es un uso que hay que registrar. Moneda es un valor de
        // TiposMoneda, igual que en DonacionDinero y GastoOperativo.
        public decimal Monto { get; set; }
        public string Moneda { get; set; } = string.Empty;

        // Cancelar no borra: el alquiler queda en el historial y deja de ocupar el
        // horario. MotivoCancelacion solo se llena al cancelar
        // (CK_AlquileresEspacio_MotivoCancelacion), mismo patrón que
        // GastoOperativo.MotivoAnulacion.
        public EstadoAlquiler Estado { get; set; } = EstadoAlquiler.Reservado;
        public string? MotivoCancelacion { get; set; }

        public string? Observaciones { get; set; }
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Inicializadas y no nullables, igual que ProyectoComunitario.Participantes.
        public List<EspacioFisico> Espacios { get; set; } = new();
        public List<CaracteristicaEspacio> Caracteristicas { get; set; } = new();
    }
}
