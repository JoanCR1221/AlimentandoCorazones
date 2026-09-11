using SIGAC.Application.DTOs.Gastos;

namespace SIGAC.Application.Interfaces
{
    public interface IGastosService
    {
        // Devuelve el Id del gasto recién creado: RegistrarGasto.razor lo necesita
        // para ofrecer el enlace a "Registrar Entrada" cuando la categoría es
        // CompraInsumos (ver AB#2501), con el gasto ya identificado en la URL.
        Task<int> RegistrarGastoAsync(GastoOperativoCrearDto dto);
        Task<GastoOperativoEditarDto?> ObtenerParaEditarAsync(int id);
        Task EditarGastoAsync(int id, GastoOperativoEditarDto dto);
        Task<GastosConsultaDto> ObtenerGastosAsync(FiltrosGastoDto filtros);
        Task AnularGastoAsync(AnulacionGastoDto dto);
    }
}
