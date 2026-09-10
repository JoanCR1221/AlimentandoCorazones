using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Donaciones;

namespace SIGAC.Application.Interfaces
{
    public interface IDonantesService
    {
        Task RegistrarDonanteAsync(DonanteCrearDto dto);

        // Devuelve null cuando el id no existe, en vez de lanzar: la pantalla de
        // edición lo traduce en "no encontrado". Mismo criterio que
        // IInventarioService.ObtenerParaEditarAsync.
        Task<DonanteEditarDto?> ObtenerParaEditarAsync(int id);

        Task ActualizarDonanteAsync(int id, DonanteEditarDto dto);

        Task<ResultadoPaginado<DonanteListaDto>> ObtenerDonantesAsync(FiltrosDonanteDto filtros);

        // Baja lógica, y no un EliminarDonanteAsync como el EliminarArticuloAsync de
        // IInventarioService: todas las FK que apuntan a Donante son Restrict
        // (DonacionesDinero, DonacionesEspecie y EntradasInventario), así que un
        // donante con historial no se puede borrar en duro — la base rechazaría el
        // DELETE. Desactivarlo lo saca de los desplegables y de los listados sin
        // tocar las donaciones que lo respaldan.
        //
        // Dos métodos con nombre propio en vez de uno con un bool: en la pantalla
        // son dos acciones distintas y así se lee qué hace cada llamada. La
        // escritura, que sí es la misma en los dos sentidos, queda unificada en
        // IDonantesRepository.CambiarEstadoAsync. Mismo reparto que
        // IBeneficiariosService.
        Task ActivarDonanteAsync(int id);
        Task DesactivarDonanteAsync(int id);
    }
}
