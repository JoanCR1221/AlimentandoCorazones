using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    // Los dos catálogos del módulo de alquileres: espacios físicos (sectores) y
    // características.
    public interface IEspaciosRepository
    {
        Task<IReadOnlyList<EspacioFisico>> ObtenerEspaciosAsync(bool soloActivos);
        Task<IReadOnlyList<EspacioFisico>> ObtenerEspaciosPorIdsAsync(IReadOnlyCollection<int> ids);
        Task<bool> ExisteNombreEspacioAsync(string nombre);

        // Lanza DuplicateException si el nombre choca contra UX_EspaciosFisicos_Nombre.
        Task AgregarEspacioAsync(EspacioFisico espacio);

        // Devuelven false si el id no existe.
        Task<bool> ActualizarCapacidadEspacioAsync(int id, int? capacidad);
        Task<bool> CambiarEstadoEspacioAsync(int id, bool estado);

        // Si algún alquiler (reservado o cancelado) usa el sector. EliminarEspacioAsync
        // no hace este chequeo por su cuenta: lo consulta el servicio antes, mismo
        // reparto que TieneMovimientosAsync/EliminarArticuloAsync en Inventario.
        Task<bool> EspacioTieneAlquileresAsync(int id);

        // Lanza ValidationException si la FK Restrict de EspaciosAlquiler rechaza el
        // DELETE (alguien lo usó en un alquiler entre el chequeo y el borrado).
        Task EliminarEspacioAsync(int id);

        Task<IReadOnlyList<CaracteristicaEspacio>> ObtenerCaracteristicasAsync(bool soloActivos);
        Task<IReadOnlyList<CaracteristicaEspacio>> ObtenerCaracteristicasPorIdsAsync(IReadOnlyCollection<int> ids);
        Task<bool> ExisteNombreCaracteristicaAsync(string nombre);

        // Lanza DuplicateException si el nombre choca contra
        // UX_CaracteristicasEspacio_Nombre.
        Task AgregarCaracteristicaAsync(CaracteristicaEspacio caracteristica);

        Task<bool> CambiarEstadoCaracteristicaAsync(int id, bool estado);

        // Mismo par que EspacioTieneAlquileresAsync/EliminarEspacioAsync.
        Task<bool> CaracteristicaTieneAlquileresAsync(int id);
        Task EliminarCaracteristicaAsync(int id);

        // El horario de alquiler configurado (la única fila de HorarioAlquiler). Si
        // la fila faltara, HorarioAlquiler.PorDefecto(): el módulo nunca queda sin
        // horario con el cual validar.
        Task<HorarioAlquiler> ObtenerHorarioAsync();

        // Reescribe la fila; la crea si no existe.
        Task GuardarHorarioAsync(HorarioAlquiler horario);
    }
}
