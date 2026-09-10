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
        /// <summary>
        /// Anula el gasto y, en la misma operación, todas las entradas de inventario
        /// que lo respaldaban, revirtiendo el stock de cada una.
        /// </summary>
        // Una sola operación y no dos, porque anular el gasto sin revertir sus
        // entradas deja el inventario afirmando que hay existencias respaldadas por
        // un gasto que oficialmente no ocurrió.
        Task AnularConEntradasVinculadasAsync(int gastoId, string motivo);
    }
}
