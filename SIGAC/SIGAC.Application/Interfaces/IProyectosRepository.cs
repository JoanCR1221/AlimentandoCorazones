using SIGAC.Application.DTOs.Proyectos;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IProyectosRepository
    {
        Task AgregarAsync(ProyectoComunitario proyecto);
        Task<ProyectoComunitario?> ObtenerPorIdAsync(int id);

        // El proyecto con sus participantes y, para los que son beneficiarios, la
        // ficha del beneficiario (nombre, teléfono y si sigue activo).
        Task<ProyectoComunitario?> ObtenerConParticipantesAsync(int id);

        Task ActualizarAsync(ProyectoComunitario proyecto);
        Task<IEnumerable<ProyectoComunitario>> ObtenerTodosAsync(FiltrosProyectoDto filtros);
        Task FinalizarAsync(int id);
        Task AgregarParticipanteAsync(ParticipanteProyecto participante);
        Task<bool> ExisteParticipanteAsync(int proyectoId, int beneficiarioId);

        // Quita un participante del proyecto. Devuelve false si no existe o si
        // pertenece a otro proyecto (el id solo no alcanza: se exigen los dos).
        Task<bool> QuitarParticipanteAsync(int proyectoId, int participanteId);
    }
}
