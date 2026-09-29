using SIGAC.Application.DTOs.Alquileres;

namespace SIGAC.Application.Interfaces
{
    // Configuración de los sectores del local y de las características que se
    // pueden pedir en un alquiler.
    public interface IEspaciosService
    {
        Task<IReadOnlyList<EspacioFisicoDto>> ObtenerEspaciosAsync(bool soloActivos);
        Task RegistrarEspacioAsync(EspacioFisicoCrearDto dto);
        Task ActualizarCapacidadEspacioAsync(int id, int? capacidad);
        Task CambiarEstadoEspacioAsync(int id, bool estado);

        // Solo si nunca se usó en un alquiler; si ya se usó, ValidationException
        // (hay que desactivarlo). Mismo criterio que EliminarArticuloAsync.
        Task EliminarEspacioAsync(int id);

        Task<IReadOnlyList<CaracteristicaDto>> ObtenerCaracteristicasAsync(bool soloActivos);
        Task RegistrarCaracteristicaAsync(CaracteristicaCrearDto dto);
        Task CambiarEstadoCaracteristicaAsync(int id, bool estado);
        Task EliminarCaracteristicaAsync(int id);

        // Horario en que se puede alquilar. Cambiarlo afecta solo a los alquileres
        // que se registren después.
        Task<HorarioAlquilerDto> ObtenerHorarioAsync();
        Task ActualizarHorarioAsync(HorarioAlquilerDto dto);
    }
}
