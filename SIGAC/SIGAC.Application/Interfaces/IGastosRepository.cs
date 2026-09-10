using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IGastosRepository
    {
        Task AgregarAsync(GastoOperativo gasto);
    }
}
