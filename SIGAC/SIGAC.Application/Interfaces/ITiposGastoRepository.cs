using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface ITiposGastoRepository
    {
        // Ordenados por nombre. soloActivos = false trae también los desactivados
        // (pantalla de administración y filtro del listado de gastos).
        Task<IReadOnlyList<TipoGasto>> ObtenerTodosAsync(bool soloActivos);
        Task<TipoGasto?> ObtenerPorIdAsync(int id);
        Task AgregarAsync(TipoGasto tipo);

        // Nombre, GeneraInventario y CuentaContablePorDefecto. Activo se mueve
        // solo por CambiarEstadoAsync.
        Task ActualizarAsync(TipoGasto tipo);
        Task CambiarEstadoAsync(int id, bool activo);
    }
}
