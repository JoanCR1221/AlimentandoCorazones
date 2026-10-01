using SIGAC.Application.DTOs.Gastos;

namespace SIGAC.Application.Interfaces
{
    // Administración del catálogo de tipos de gasto (/gastos/tipos). Los
    // formularios de gasto no lo usan: piden los tipos activos a IGastosService.
    public interface ITiposGastoService
    {
        // Activos e inactivos, ordenados por nombre.
        Task<IReadOnlyList<TipoGastoDto>> ObtenerTodosAsync();
        Task RegistrarAsync(TipoGastoGuardarDto dto);
        Task EditarAsync(int id, TipoGastoGuardarDto dto);

        // Baja lógica, nunca borrado: los gastos registrados apuntan al tipo.
        Task ActivarAsync(int id);
        Task DesactivarAsync(int id);
    }
}
