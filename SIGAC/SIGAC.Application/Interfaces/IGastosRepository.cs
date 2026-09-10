using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IGastosRepository
    {
        Task AgregarAsync(GastoOperativo gasto);
        Task<GastoOperativo?> ObtenerPorIdAsync(int id);
        Task ActualizarAsync(GastoOperativo gasto);
    }
}
