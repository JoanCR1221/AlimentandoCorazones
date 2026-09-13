namespace SIGAC.Domain.Entities
{
    public enum EstadoProyecto
    {
        Planificado,
        EnCurso,
        Finalizado,
        Cancelado
    }

    // Proyecto o actividad comunitaria organizada por la asociación (taller, curso,
    // campaña de salud, etc.). Los participantes vinculados viven en
    // ParticipanteProyecto y no en columnas de esta entidad: un proyecto puede tener
    // cualquier cantidad de participantes.
    public class ProyectoComunitario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaEstimadaFin { get; set; }

        // Solo se llena al finalizar (FinalizarProyectoAsync), igual que
        // MotivoAnulacion en GastoOperativo: un proyecto Planificado, EnCurso o
        // Cancelado la deja en NULL.
        public DateTime? FechaFinalizacionReal { get; set; }

        public EstadoProyecto Estado { get; set; } = EstadoProyecto.Planificado;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Inicializada y no nullable, igual que DonacionEspecie.Detalles: recorrer
        // los participantes de un proyecto recién creado no debe obligar a
        // comprobar null antes de agregar el primero.
        public List<ParticipanteProyecto> Participantes { get; set; } = new();
    }
}
