using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IDonacionesRepository
    {
        // Escrituras
        //
        // Ninguno de estos métodos toca el stock, a diferencia de los
        // RegistrarEntradaConStockAsync / RegistrarSalidaConStockAsync de
        // IInventarioRepository: Inventario sigue siendo el dueño del stock. El
        // servicio de Donaciones guarda acá el "quién y por qué" y delega en
        // IInventarioService el movimiento que ajusta las existencias.

        Task AgregarDonacionDineroAsync(DonacionDinero donacion);

        // Guarda la cabecera y sus líneas de detalle en una sola operación: una
        // cabecera sin detalle no registra nada, así que no pueden confirmarse por
        // separado (es el mismo motivo por el que las operaciones de Inventario que
        // mueven stock son compuestas).
        Task AgregarDonacionEspecieAsync(DonacionEspecie donacion);

        Task AgregarDonacionEntregadaAsync(DonacionEntregada donacion);

        // Consultas de historial
        //
        // Dinero y especie se consultan por SEPARADO y no en un solo método, porque
        // son dos tablas con forma distinta: una tiene Monto y la otra una colección
        // de detalles. Unirlas en SQL obligaría a un UNION sobre columnas que no se
        // corresponden, y a rellenar con NULL las que le faltan a cada lado.
        //
        // El servicio es el que las une: proyecta cada una a HistorialDonacionDto
        // (que sí tiene forma común), las ordena juntas y calcula TotalDinero. Así
        // el repositorio devuelve entidades del dominio, como en el resto del
        // proyecto, y la forma de presentación queda de un solo lado.

        Task<IEnumerable<DonacionDinero>> ObtenerDonacionesDineroAsync(FiltrosHistorialDonacionDto filtros);

        // Debe traer los Detalles cargados: el servicio arma con ellos la
        // Descripcion de la fila del historial, y sin incluirlos explícitamente la
        // colección llega vacía (no hay lazy loading configurado en el proyecto).
        Task<IEnumerable<DonacionEspecie>> ObtenerDonacionesEspecieAsync(FiltrosHistorialDonacionDto filtros);

        // Recibe los filtros ya tipados y no int?/DateTime? sueltos como
        // ObtenerEntradasAsync/ObtenerSalidasAsync de Inventario: son cinco
        // criterios y una lista de parámetros posicionales de ese largo se presta a
        // pasarlos cambiados de orden.
        Task<IEnumerable<DonacionEntregada>> ObtenerEntregasAsync(FiltrosHistorialEntregaDto filtros);
    }
}
