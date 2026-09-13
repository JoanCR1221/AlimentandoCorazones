using SIGAC.Application.DTOs.Proyectos;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IProyectosRepository
    {
        Task AgregarAsync(ProyectoComunitario proyecto);
        Task<ProyectoComunitario?> ObtenerPorIdAsync(int id);
        Task ActualizarAsync(ProyectoComunitario proyecto);
        Task<IEnumerable<ProyectoComunitario>> ObtenerTodosAsync(FiltrosProyectoDto filtros);
        Task FinalizarAsync(int id);

        Task AgregarParticipanteAsync(ParticipanteProyecto participante);
        Task<bool> ExisteParticipanteAsync(int proyectoId, int beneficiarioId);
    }
}
