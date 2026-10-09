using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.DTOs.Reportes;
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
        // Sin paginar: lo usan el reporte de alquileres y la ocupación del día al
        // registrar, que necesitan el conjunto completo.
        Task<IReadOnlyList<AlquilerEspacio>> ObtenerHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros);

        // Una página del calendario (filtros.Pagina / TamanoPagina), con los mismos
        // filtros y el mismo orden que ObtenerHistorialAlquileresAsync, más cuántos
        // alquileres cumplen el filtro en total.
        Task<ResultadoPaginado<AlquilerEspacio>> ObtenerPaginaHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros);

        // Ingreso por moneda de los alquileres RESERVADOS que cumplen el filtro: uno
        // cancelado no genera ingreso. Cubre TODO el conjunto filtrado e ignora la
        // paginación.
        Task<IReadOnlyList<MontoPorMonedaDto>> ObtenerTotalesPorMonedaAsync(FiltrosHistorialAlquilerDto filtros);

        // Pasa a Cancelado solo si sigue Reservado. Devuelve false si no existe o si
        // ya estaba cancelado (otro usuario se adelantó).
        Task<bool> CancelarAsync(int id, string motivoCancelacion);

        // Para el panorama gráfico (Reportes): los alquileres, reservados y
        // cancelados, de los últimos mesesHaciaAtras meses calendario contando el mes
        // actual como el primero y hasta el final de ese mes. Solo las columnas que
        // el panorama agrupa y los nombres de los sectores, sin arrendatario ni
        // características. Sin orden garantizado.
        Task<IReadOnlyList<AlquilerPanoramaDto>> ObtenerParaPanoramaAsync(int mesesHaciaAtras);
    }
}
