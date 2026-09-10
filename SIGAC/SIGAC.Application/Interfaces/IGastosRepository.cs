using SIGAC.Application.DTOs.Gastos;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IGastosRepository
    {
        Task AgregarAsync(GastoOperativo gasto);
        Task<GastoOperativo?> ObtenerPorIdAsync(int id);
        Task ActualizarAsync(GastoOperativo gasto);
        Task<IEnumerable<GastoOperativo>> ObtenerTodosAsync(FiltrosGastoDto filtros);
        Task AnularAsync(int id, string motivo);
    }
}
