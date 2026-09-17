using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    // Bitácora de acciones (PBI 1949). Solo inserción y consulta, a propósito:
    // no hay actualizar ni borrar porque nadie puede alterar el historial, y la
    // base lo respalda con el trigger TR_Bitacora_SoloInsercion.
    public interface IBitacoraRepository
    {
        Task RegistrarAccionAsync(BitacoraAccion accion);

        // Una página, filtrada y ordenada en SQL (fecha descendente), con el
        // total para el paginador.
        Task<ResultadoPaginado<BitacoraAccion>> ObtenerAccionesAsync(FiltrosBitacoraDto filtros);
    }
}
