using Microsoft.EntityFrameworkCore;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Descuento de stock compartido por los dos repositorios que lo necesitan:
    // InventarioRepositoryEfCore (salidas y aprobación de préstamos) y
    // GastosRepositoryEfCore (anular un gasto revierte las entradas que lo
    // respaldaban). Vive acá y no dentro de uno de los dos para que la condición
    // "solo si alcanza" sea literalmente el mismo UPDATE en todos los caminos.
    internal static class StockArticulos
    {
        /// <summary>
        /// Descuenta <paramref name="cantidad"/> del stock del artículo, solo si
        /// alcanza. Devuelve false si no se pudo (artículo inexistente o stock
        /// insuficiente), sin tocar la fila.
        /// </summary>
        // Devuelve bool en vez de lanzar: por qué no alcanzó el stock no significa lo
        // mismo en todos los caminos —concurrencia al registrar un movimiento, o una
        // entrada ya consumida al anular un gasto— y el mensaje que ve el usuario lo
        // arma quien llama. Antes esto lanzaba siempre el texto de concurrencia, que
        // en la anulación de un gasto describía una causa que no era la real.
        //
        // El filtro StockActual >= cantidad va en el propio UPDATE y no en un chequeo
        // previo: así la comprobación y el descuento son la misma operación atómica y
        // no hay ventana entre leer el stock y modificarlo.
        public static async Task<bool> IntentarDescontarAsync(SigacDbContext context, int articuloId, int cantidad)
        {
            var filasAfectadas = await context.Articulos
                .Where(a => a.Id == articuloId && a.StockActual >= cantidad)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.StockActual, a => a.StockActual - cantidad));

            return filasAfectadas > 0;
        }
    }
}
