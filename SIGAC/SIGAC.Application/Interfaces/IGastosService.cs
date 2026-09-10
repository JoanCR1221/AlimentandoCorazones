using SIGAC.Application.DTOs.Gastos;

namespace SIGAC.Application.Interfaces
{
    public interface IGastosService
    {
        Task RegistrarGastoAsync(GastoOperativoCrearDto dto);
    }
}
