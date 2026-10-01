using SIGAC.Application.DTOs.Gastos;

namespace SIGAC.Application.Interfaces
{
    public interface IGastosService
    {
        // Devuelve el Id del gasto recién creado: RegistrarGasto.razor lo necesita
        // para ofrecer el enlace a "Registrar Entrada" cuando el tipo de gasto
        // genera inventario (ver AB#2501), con el gasto ya identificado en la URL.
        Task<int> RegistrarGastoAsync(GastoOperativoCrearDto dto);
        Task<GastoOperativoEditarDto?> ObtenerParaEditarAsync(int id);
        Task EditarGastoAsync(int id, GastoOperativoEditarDto dto);
        Task<GastosConsultaDto> ObtenerGastosAsync(FiltrosGastoDto filtros);
        Task AnularGastoAsync(AnulacionGastoDto dto);

        // Tipos para el desplegable del formulario, ordenados por nombre. Solo los
        // activos, más el indicado en incluirTipoId aunque esté inactivo: al editar
        // un gasto cuyo tipo se desactivó después, el desplegable tiene que poder
        // mostrar el tipo que el gasto ya tiene.
        Task<IReadOnlyList<TipoGastoDto>> ObtenerTiposActivosAsync(int? incluirTipoId = null);

        // Proveedores ya usados en algún gasto que contienen el texto (sin
        // distinguir tildes ni mayúsculas), para el autocompletado del formulario.
        Task<IReadOnlyList<string>> BuscarProveedoresAsync(string texto);
    }
}
