using SIGAC.Application.DTOs.Proyectos;

namespace SIGAC.Application.Interfaces
{
    public interface IProyectosService
    {
        // Devuelve el Id del proyecto recién creado, mismo criterio que
        // GastosService.RegistrarGastoAsync.
        Task<int> RegistrarProyectoAsync(ProyectoCrearDto dto);
        Task EditarProyectoAsync(int id, ProyectoEditarDto dto);
        Task<IReadOnlyList<ProyectoListaDto>> ObtenerProyectosAsync(FiltrosProyectoDto filtros);
        Task FinalizarProyectoAsync(int id);

        Task RegistrarParticipanteAsync(ParticipanteCrearDto dto);
    }
}
