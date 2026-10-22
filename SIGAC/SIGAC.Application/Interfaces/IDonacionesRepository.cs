using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    // Una fila del historial unificado: o una donación en dinero o una en especie,
    // nunca las dos. Es lo que devuelve la consulta paginada, que mezcla las dos
    // tablas en una sola lista ordenada.
    public sealed record ItemHistorialDonacion(DonacionDinero? Dinero, DonacionEspecie? Especie);

    public interface IDonacionesRepository
    {
        // Escrituras
        //
        // Cada una es COMPUESTA a propósito, por el mismo motivo que las de
        // IInventarioRepository: agrupa todo lo que un registro necesita para que se
        // escriba todo-o-nada. Antes el donante nuevo, la donación y el movimiento de
        // stock eran escrituras sueltas y una falla a mitad de camino dejaba un donante
        // huérfano, una donación sin stock o un stock descontado sin entrega.
        //
        // Inventario sigue siendo el dueño de las REGLAS del stock (qué artículo,
        // cuánto, si alcanza), que decide IInventarioService antes de llegar acá. Lo
        // que se comparte es solo la transacción.

        /// <summary>
        /// Guarda la donación. Si <paramref name="nuevoDonante"/> no es null, lo crea en
        /// la misma transacción y le asigna su Id a la donación.
        /// </summary>
        Task AgregarDonacionDineroAsync(DonacionDinero donacion, Donante? nuevoDonante);

        /// <summary>
        /// Guarda la cabecera con sus líneas, el donante nuevo (si hay) y las entradas
        /// de inventario con su suma al stock, todo en una sola transacción. Las
        /// <paramref name="entradas"/> vienen ya validadas (ver
        /// IInventarioService.PrepararEntradasAsync): el repositorio les completa el
        /// donante y la referencia a la donación.
        /// </summary>
        Task AgregarDonacionEspecieAsync(
            DonacionEspecie donacion, Donante? nuevoDonante, IReadOnlyList<EntradaInventario> entradas);

        /// <summary>
        /// Guarda la entrega y su salida de inventario, y descuenta el stock, todo en una
        /// sola transacción. Si el stock ya no alcanza (otro usuario lo consumió entre la
        /// validación y este momento) no queda nada escrito y lanza ValidationException.
        /// </summary>
        Task AgregarDonacionEntregadaAsync(DonacionEntregada donacion, SalidaInventario salida);

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

        // Historial paginado: UNA página del historial unificado (dinero y especie
        // mezcladas), de la más reciente a la más antigua, más cuántas donaciones
        // cumplen el filtro en total.
        //
        // La unión en SQL es solo de las CLAVES (tipo, Id, fecha), que sí tienen la
        // misma forma en las dos tablas: eso da el orden y el Skip/Take globales. Las
        // filas completas, con su forma distinta, se traen aparte y solo para las
        // donaciones de esa página, así que el motivo por el que dinero y especie se
        // consultan por separado sigue en pie. Ignora los reportes: ellos usan las dos
        // consultas de arriba, sin paginar.
        Task<ResultadoPaginado<ItemHistorialDonacion>> ObtenerPaginaHistorialAsync(FiltrosHistorialDonacionDto filtros);

        // Suma del dinero por moneda sobre TODAS las donaciones que cumplen el filtro
        // (ignora la paginación). Las donaciones en especie no tienen monto: no aportan.
        Task<IReadOnlyList<MontoPorMonedaDto>> ObtenerTotalesDineroPorMonedaAsync(FiltrosHistorialDonacionDto filtros);

        // Recibe los filtros ya tipados y no int?/DateTime? sueltos como
        // ObtenerEntradasAsync/ObtenerSalidasAsync de Inventario: son cinco
        // criterios y una lista de parámetros posicionales de ese largo se presta a
        // pasarlos cambiados de orden.
        Task<IEnumerable<DonacionEntregada>> ObtenerEntregasAsync(FiltrosHistorialEntregaDto filtros);

        // Consultas del panorama gráfico (Reportes)
        //
        // Cifras agregadas en SQL, sobre una ventana de meses calendario hacia atrás
        // que incluye el mes actual. Solo traen los meses que tienen donaciones: la
        // pantalla alinea el resultado contra la ventana completa.

        // Cantidad de donaciones por mes y clase ("Dinero" o "Especie").
        Task<IReadOnlyList<ConteoDonacionMensualDto>> ObtenerCantidadPorMesAsync(int mesesHaciaAtras);

        // Dinero recibido por mes, solo en colones: sumar monedas distintas en un
        // mismo número no representa nada.
        Task<IReadOnlyList<MontoPorMesDto>> ObtenerDineroEnColonesPorMesAsync(int mesesHaciaAtras);

        // Los donantes con más donaciones (dinero y especie juntas), de mayor a
        // menor; en empate, por nombre.
        Task<IReadOnlyList<DonacionesPorDonanteDto>> ObtenerTopDonantesAsync(int mesesHaciaAtras, int maximo);
    }
}
