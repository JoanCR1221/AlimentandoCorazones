using SIGAC.Application.DTOs.Proyectos;
using SIGAC.Application.DTOs.Reportes;
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

        // Para Reportes: los proyectos con sus participantes (sin el beneficiario
        // completo, solo los campos del propio participante), filtrados por estado y
        // por el rango de su fecha de INICIO. Sin paginación, mismo criterio que
        // ObtenerTodosAsync. Orden: fecha de inicio, de la más reciente a la más
        // antigua, igual que el listado.
        Task<IReadOnlyList<ProyectoComunitario>> ObtenerParaReporteAsync(FiltrosReporteProyectosDto filtros);
    }
}
