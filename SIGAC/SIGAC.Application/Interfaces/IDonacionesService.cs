using SIGAC.Application.DTOs.Donaciones;

namespace SIGAC.Application.Interfaces
{
    public interface IDonacionesService
    {
        // Registro de donaciones RECIBIDAS
        //
        // Las dos primeras resuelven el par excluyente DonanteId / NuevoDonante del
        // DTO: si viene NuevoDonante, dan de alta el donante y usan su Id; si viene
        // DonanteId, verifican que exista. Que llegue exactamente uno de los dos no
        // lo puede validar una DataAnnotation (mira dos propiedades a la vez), así
        // que es responsabilidad de estos métodos.

        Task RegistrarDonacionDineroAsync(DonacionDineroCrearDto dto);

        // Además de guardar la donación, es la que delega en IInventarioService para
        // que lo donado entre al stock como EntradaInventario con origen "Donacion".
        // Inventario sigue siendo el dueño del stock: acá no se toca StockActual.
        Task RegistrarDonacionEspecieAsync(DonacionEspecieCrearDto dto);

        // Registro de donaciones ENTREGADAS
        //
        // Valida la exclusión mutua del destinatario (Beneficiario o Comunidad,
        // nunca los dos ni ninguno) antes de guardar, y delega en IInventarioService
        // el descuento del stock, que queda registrado como SalidaInventario de tipo
        // "Donacion" apuntando a esta entrega.
        Task RegistrarDonacionEntregadaAsync(DonacionEntregadaCrearDto dto);

        // Historiales
        //
        // Unifica en una sola lista lo que el repositorio devuelve en dos consultas
        // separadas (dinero y especie), ordenado por fecha, y calcula el total de
        // dinero sobre TODAS las donaciones que cumplen el filtro. Ver el comentario
        // de IDonacionesRepository sobre por qué la unión se hace acá y no en SQL.
        Task<HistorialDonacionesResultadoDto> ObtenerHistorialDonacionesAsync(FiltrosHistorialDonacionDto filtros);

        // Resuelve el nombre del destinatario para cada fila: el del beneficiario o
        // el de la comunidad, según TipoDestinatario. La grilla recibe una sola
        // columna de texto y no tiene que saber de cuál de los dos campos salió.
        Task<HistorialEntregasResultadoDto> ObtenerHistorialEntregasAsync(FiltrosHistorialEntregaDto filtros);
    }
}
