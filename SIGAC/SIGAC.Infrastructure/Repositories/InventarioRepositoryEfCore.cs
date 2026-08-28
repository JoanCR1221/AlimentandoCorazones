using Microsoft.EntityFrameworkCore;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación real del repositorio con EF Core sobre SQL Server.
    // Respeta el contrato de IInventarioRepository sin cambiar su firma.
    // Reemplaza a InventarioRepositoryEnMemoria, que perdía todo al reiniciar.
    public class InventarioRepositoryEfCore : IInventarioRepository
    {
        // Collation acentuada-insensible (AI) para la búsqueda de texto: hace que
        // "Azucar" encuentre a "Azúcar" sin salir de SQL. Se aplica a la expresión,
        // no a la columna, así que no depende de la collation de la base.
        private const string ColacionSinTildes = "Latin1_General_CI_AI";

        // Factory y no un DbContext inyectado: en Blazor Server el scope dura toda
        // la sesión, así que un contexto compartido queda expuesto a que dos
        // operaciones lo usen a la vez (por ejemplo, el listado de existencias
        // recargando mientras se registra una entrada), y DbContext no tolera eso.
        // Cada método pide su propio contexto de corta vida.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public InventarioRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ------------------------------------------------------------------
        // Artículos
        // ------------------------------------------------------------------

        public async Task<Articulo?> ObtenerArticuloPorNombreAsync(string nombre)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Igualdad directa y SIN collation explícita: es exactamente la misma
            // comparación que hace el índice único UX_Articulos_Nombre, así que el
            // código y la base coinciden en qué es "el mismo artículo" y la consulta
            // puede hacer seek sobre ese índice. Con Collate AI se encontrarían filas
            // que el índice considera distintas, y el servicio daría por existente un
            // artículo que al insertarse no chocaría con nada.
            // El "sin distinguir mayúsculas" lo aporta la collation CI de SQL Server.
            return await context.Articulos
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Nombre == nombre);
        }

        public async Task<Articulo?> ObtenerArticuloPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Articulos
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task AgregarArticuloAsync(Articulo articulo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Articulos.Add(articulo);

            // Tras el SaveChanges, EF Core escribe el Id generado en la misma entidad
            // que recibió. El servicio depende de eso: usa articulo.Id para armar la
            // entrada de inventario inmediatamente después de llamar a este método.
            await context.SaveChangesAsync();
        }

        public async Task ActualizarArticuloAsync(Articulo articulo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existente = await context.Articulos
                .FirstOrDefaultAsync(a => a.Id == articulo.Id);

            if (existente is null)
                return;

            // Se copian los campos del catálogo uno por uno en vez de usar Update():
            // Update() marcaría TODAS las columnas como modificadas, incluida
            // StockActual, y reescribiría el valor que traía la entidad desprendida.
            // Como el stock se mueve por otro camino (ActualizarStockAsync /
            // ReducirStockAsync), una entrada registrada entre la lectura y el
            // guardado quedaría pisada.
            //
            // Por eso StockActual queda deliberadamente afuera: en este repositorio
            // el stock SOLO cambia a través de los métodos de stock.
            existente.Nombre = articulo.Nombre;
            existente.Categoria = articulo.Categoria;
            existente.UnidadMedida = articulo.UnidadMedida;
            existente.StockMinimo = articulo.StockMinimo;

            await context.SaveChangesAsync();
        }

        public async Task ActualizarStockAsync(int articuloId, int cantidad)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // ExecuteUpdate y no leer-modificar-guardar: se traduce a un solo
            // UPDATE Articulos SET StockActual = StockActual + @cantidad, que la base
            // resuelve de forma atómica. Con la versión leída en memoria, dos entradas
            // simultáneas del mismo artículo leerían el mismo stock inicial y una
            // pisaría a la otra (lost update).
            await context.Articulos
                .Where(a => a.Id == articuloId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.StockActual, a => a.StockActual + cantidad));
        }

        public async Task ReducirStockAsync(int articuloId, int cantidad)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // La condición "hay stock suficiente" viaja dentro del WHERE del UPDATE en
            // vez de evaluarse antes: así el chequeo y el descuento son la misma
            // operación y dos salidas simultáneas no pueden dejar el stock negativo.
            // El CHECK CK_Articulos_StockActual_NoNegativo es la última red de todos modos.
            var filasAfectadas = await context.Articulos
                .Where(a => a.Id == articuloId && a.StockActual >= cantidad)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.StockActual, a => a.StockActual - cantidad));

            // Cero filas significa que el artículo no existe o que el stock ya no
            // alcanza, aunque el servicio lo hubiera verificado un instante antes.
            // Se falla en voz alta en vez de no hacer nada: la salida ya quedó
            // insertada por la llamada anterior, y callarse dejaría el movimiento
            // registrado sin su descuento.
            if (filasAfectadas == 0)
            {
                throw new InvalidOperationException(
                    "No se pudo descontar el stock: el artículo no existe o el stock disponible cambió.");
            }
        }

        public async Task<IEnumerable<Articulo>> ObtenerExistenciasAsync(string? nombre, string? categoria)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var consulta = context.Articulos.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(nombre))
            {
                // Acá sí va la collation AI: esto es la caja de búsqueda del listado,
                // no la resolución de la clave natural, y quien escribe "azucar"
                // espera encontrar "Azúcar".
                var busqueda = nombre.Trim();
                consulta = consulta.Where(a =>
                    EF.Functions.Collate(a.Nombre, ColacionSinTildes).Contains(busqueda));
            }

            if (!string.IsNullOrWhiteSpace(categoria))
            {
                // Igualdad exacta: la categoría se elige de una lista, no se teclea.
                // Cubierta por el índice IX_Articulos_Categoria.
                var categoriaFiltro = categoria;
                consulta = consulta.Where(a => a.Categoria == categoriaFiltro);
            }

            // ToListAsync obligatorio: el contexto se libera al salir del método y un
            // IQueryable diferido explotaría al recorrerlo desde la página.
            return await consulta
                .OrderBy(a => a.Nombre)
                .ToListAsync();
        }

        // ------------------------------------------------------------------
        // Entradas y salidas
        // ------------------------------------------------------------------

        public async Task AgregarEntradaAsync(EntradaInventario entrada)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.EntradasInventario.Add(entrada);
            await context.SaveChangesAsync();
        }

        public async Task AgregarSalidaAsync(SalidaInventario salida)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.SalidasInventario.Add(salida);
            await context.SaveChangesAsync();
        }

        public async Task<IEnumerable<EntradaInventario>> ObtenerEntradasAsync(int? articuloId, DateTime? desde, DateTime? hasta)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Include trae el artículo en el mismo viaje: sin él la navegación llega
            // en null y el historial mostraría el nombre del artículo vacío.
            IQueryable<EntradaInventario> consulta = context.EntradasInventario
                .AsNoTracking()
                .Include(e => e.Articulo);

            if (articuloId.HasValue)
            {
                var id = articuloId.Value;
                consulta = consulta.Where(e => e.ArticuloId == id);
            }

            // Las fechas se normalizan acá y no dentro de la expresión: si el .Date
            // quedara del lado de la columna, EF traduciría CONVERT(date, [e].[Fecha])
            // y el índice de Fecha dejaría de poder usarse.
            if (desde.HasValue)
            {
                var inicio = desde.Value.Date;
                consulta = consulta.Where(e => e.Fecha >= inicio);
            }

            // Límite superior "menor que el día siguiente" y no "menor o igual que
            // hasta": la columna es datetime2 y guarda la hora, así que un movimiento
            // de las 14:30 del último día quedaría fuera si se comparara contra su
            // medianoche.
            if (hasta.HasValue)
            {
                var finExclusivo = hasta.Value.Date.AddDays(1);
                consulta = consulta.Where(e => e.Fecha < finExclusivo);
            }

            // El orden se fija en SQL para que el resultado sea estable entre llamadas
            // con los mismos filtros.
            return await consulta
                .OrderByDescending(e => e.Fecha)
                .ThenByDescending(e => e.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<SalidaInventario>> ObtenerSalidasAsync(int? articuloId, DateTime? desde, DateTime? hasta)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            IQueryable<SalidaInventario> consulta = context.SalidasInventario
                .AsNoTracking()
                .Include(s => s.Articulo);

            if (articuloId.HasValue)
            {
                var id = articuloId.Value;
                consulta = consulta.Where(s => s.ArticuloId == id);
            }

            // Mismo tratamiento de fechas que en las entradas: el historial de
            // movimientos consulta las dos tablas con los mismos criterios.
            if (desde.HasValue)
            {
                var inicio = desde.Value.Date;
                consulta = consulta.Where(s => s.Fecha >= inicio);
            }

            if (hasta.HasValue)
            {
                var finExclusivo = hasta.Value.Date.AddDays(1);
                consulta = consulta.Where(s => s.Fecha < finExclusivo);
            }

            return await consulta
                .OrderByDescending(s => s.Fecha)
                .ThenByDescending(s => s.Id)
                .ToListAsync();
        }

        // ------------------------------------------------------------------
        // Préstamos
        // ------------------------------------------------------------------

        public async Task AgregarSolicitudPrestamoAsync(SolicitudPrestamo solicitud)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.SolicitudesPrestamo.Add(solicitud);
            await context.SaveChangesAsync();
        }

        public async Task<SolicitudPrestamo?> ObtenerSolicitudPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // A propósito SIN Include del artículo: quien llama a este método
            // (AprobarPrestamoAsync / RechazarPrestamoAsync) modifica la solicitud y
            // se la devuelve a ActualizarSolicitudAsync. Si viniera con el artículo
            // colgado, el guardado podría arrastrar también esa fila y pisarle el
            // stock. El servicio pide el artículo por separado cuando lo necesita.
            return await context.SolicitudesPrestamo
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task ActualizarSolicitudAsync(SolicitudPrestamo solicitud)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existente = await context.SolicitudesPrestamo
                .FirstOrDefaultAsync(s => s.Id == solicitud.Id);

            if (existente is null)
                return;

            // Solo los campos de resolución, que son los únicos que el servicio cambia
            // después de crear la solicitud. Artículo, cantidad, actividad y
            // solicitante son el pedido original y no se reescriben.
            existente.Estado = solicitud.Estado;
            existente.MotivoRechazo = solicitud.MotivoRechazo;

            await context.SaveChangesAsync();
        }

        public async Task<IEnumerable<SolicitudPrestamo>> ObtenerSolicitudesAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Acá sí va el Include: la lista muestra el nombre del artículo y las
            // entidades salen en AsNoTracking, así que no hay riesgo de arrastrarlas
            // a un guardado posterior.
            return await context.SolicitudesPrestamo
                .AsNoTracking()
                .Include(s => s.Articulo)
                .OrderByDescending(s => s.Fecha)
                .ThenByDescending(s => s.Id)
                .ToListAsync();
        }
    }
}
