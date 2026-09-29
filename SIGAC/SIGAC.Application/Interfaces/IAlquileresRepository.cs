using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IAlquileresRepository
    {
        // Inserta el alquiler con sus sectores y características. Vuelve a buscar
        // choques DENTRO de la misma transacción y con el día bloqueado: entre el
        // chequeo del servicio y este INSERT otro usuario pudo reservar el mismo
        // sector. Si pasó, lanza ValidationException y no guarda nada.
        //
        // alquiler.Espacios y alquiler.Caracteristicas llevan entidades que ya
        // existen (el servicio las leyó): se vinculan, no se crean.
        Task AgregarAlquilerAsync(AlquilerEspacio alquiler);

        // Alquileres Reservados del mismo día que se traslapan con la franja y usan
        // alguno de los sectores indicados. Traen arrendatario y sectores para
        // armar el mensaje.
        Task<IReadOnlyList<AlquilerEspacio>> ObtenerChoquesAsync(
            DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin, IReadOnlyCollection<int> espacioIds);

        Task<AlquilerEspacio?> ObtenerPorIdAsync(int id);

        // Filtros dinámicos compuestos sobre el IQueryable, con arrendatario,
        // sectores y características incluidos. Orden: fecha y hora de inicio.
        Task<IReadOnlyList<AlquilerEspacio>> ObtenerHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros);

        // Pasa a Cancelado solo si sigue Reservado. Devuelve false si no existe o si
        // ya estaba cancelado (otro usuario se adelantó).
        Task<bool> CancelarAsync(int id, string motivoCancelacion);
    }
}
