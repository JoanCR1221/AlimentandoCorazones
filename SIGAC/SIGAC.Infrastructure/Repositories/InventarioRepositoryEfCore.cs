using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.Exceptions;
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

        // Números de error de SQL Server para violación de unicidad: 2627 es una
        // restricción UNIQUE/PK y 2601 un índice único. Se usan para traducir el
        // choque contra UX_Articulos_Nombre y UX_SalidasInventario_SolicitudPrestamo
        // a excepciones con mensaje entendible en vez de un DbUpdateException crudo.
        private const int ErrorSqlRestriccionUnica = 2627;
        private const int ErrorSqlIndiceUnico = 2601;

        // 547 es el conflicto con una restricción: cubre tanto las FK como los CHECK.
        // Se usa para traducir el rechazo del DELETE de un artículo con movimientos,
        // que las FK Restrict de EntradasInventario, SalidasInventario y
        // SolicitudesPrestamo bloquean en la base.
        private const int ErrorSqlConflictoRestriccion = 547;

        // Nombres de los índices únicos de Articulos. SQL Server los incluye
        // literalmente en el texto del error 2601/2627, así que sirven para saber
        // CUÁL de los dos rechazó la escritura y dar el mensaje que corresponde.
        // El nombre del índice va en el mensaje aunque el servidor esté en otro
        // idioma, así que no depende de la localización.
        private const string IndiceUnicoNombreArticulo = "UX_Articulos_Nombre";
        private const string IndiceUnicoCodigoArticulo = "UX_Articulos_Codigo";

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
            // Como el stock se mueve por otro camino (las operaciones de movimiento
            // de más abajo), una entrada registrada entre la lectura y el guardado
            // quedaría pisada.
            //
            // Por eso StockActual queda deliberadamente afuera: en este repositorio
            // el stock SOLO cambia dentro de una operación de movimiento.
            existente.Nombre = articulo.Nombre;
            existente.Codigo = articulo.Codigo;
            existente.Categoria = articulo.Categoria;
            existente.UnidadMedida = articulo.UnidadMedida;
            existente.Ubicacion = articulo.Ubicacion;
            existente.StockMinimo = articulo.StockMinimo;

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                // Choque contra UX_Articulos_Nombre o UX_Articulos_Codigo al editar
                // hacia un valor ya usado. Antes salía como DbUpdateException y el
                // servicio la envolvía en un Exception genérico, así que el usuario no
                // sabía el motivo; y una vez traducido, el mensaje hablaba siempre del
                // nombre aunque el choque hubiera sido del código.
                throw new DuplicateException(DescribirDuplicadoDeArticulo(ex, articulo));
            }
        }

        public async Task<ResultadoPaginado<Articulo>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var consulta = context.Articulos.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filtros.Nombre))
            {
                // Acá sí va la collation AI: esto es la caja de búsqueda del listado,
                // no la resolución de la clave natural, y quien escribe "azucar"
                // espera encontrar "Azúcar". Código entra en la misma caja (sin
                // collation: es un identificador corto, no texto para acentuar) para
                // que buscar "P001" encuentre el artículo sin cambiar de campo.
                var busqueda = filtros.Nombre.Trim();
                consulta = consulta.Where(a =>
                    EF.Functions.Collate(a.Nombre, ColacionSinTildes).Contains(busqueda) ||
                    (a.Codigo != null && a.Codigo.Contains(busqueda)));
            }

            if (!string.IsNullOrWhiteSpace(filtros.Categoria))
            {
                // Igualdad exacta: la categoría se elige de una lista, no se teclea.
                // Cubierta por el índice IX_Articulos_Categoria.
                var categoriaFiltro = filtros.Categoria;
                consulta = consulta.Where(a => a.Categoria == categoriaFiltro);
            }

            // Consulta 1: cuántos artículos cumplen los filtros, para que la grilla
            // sepa cuántas páginas hay.
            var total = await consulta.CountAsync();

            // Consulta 2: solo la página pedida. ToListAsync obligatorio: el contexto
            // se libera al salir del método y un IQueryable diferido explotaría al
            // recorrerlo desde la página.
            var elementos = await consulta
                .OrderBy(a => a.Nombre)
                .Skip(filtros.PaginaEfectiva * filtros.TamanoPaginaEfectivo)
                .Take(filtros.TamanoPaginaEfectivo)
                .ToListAsync();

            return new ResultadoPaginado<Articulo>(elementos, total);
        }

        public async Task<bool> ExisteNombreAsync(string nombre, int? idExcluir = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Igualdad directa, sin collation: es la misma comparación que hace
            // UX_Articulos_Nombre. El "sin distinguir mayúsculas" lo aporta la
            // collation CI de SQL Server, igual que en ObtenerArticuloPorNombreAsync.
            return await context.Articulos
                .AsNoTracking()
                .AnyAsync(a => a.Nombre == nombre && (idExcluir == null || a.Id != idExcluir));
        }

        public async Task<bool> ExisteCodigoAsync(string? codigo, int? idExcluir = null)
        {
            // Sin código no hay nada que chocar: es la misma exclusión que hace el
            // filtro del índice único UX_Articulos_Codigo.
            if (string.IsNullOrEmpty(codigo))
                return false;

            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Articulos
                .AsNoTracking()
                .AnyAsync(a => a.Codigo == codigo && (idExcluir == null || a.Id != idExcluir));
        }

        public async Task<bool> TieneMovimientosAsync(int articuloId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Tres EXISTS independientes en vez de un JOIN: el artículo puede tener
            // historial en cualquiera de las tres tablas y basta con encontrar uno
            // para bloquear el borrado, así que no hace falta combinarlas.
            var tieneEntradas = await context.EntradasInventario
                .AsNoTracking()
                .AnyAsync(e => e.ArticuloId == articuloId);

            if (tieneEntradas)
                return true;

            var tieneSalidas = await context.SalidasInventario
                .AsNoTracking()
                .AnyAsync(s => s.ArticuloId == articuloId);

            if (tieneSalidas)
                return true;

            return await context.SolicitudesPrestamo
                .AsNoTracking()
                .AnyAsync(s => s.ArticuloId == articuloId);
        }

        public async Task EliminarArticuloAsync(int articuloId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var articulo = await context.Articulos
                .FirstOrDefaultAsync(a => a.Id == articuloId);

            if (articulo is null)
                return;

            // Sin chequeo de movimientos acá a propósito: es responsabilidad de quien
            // llama (TieneMovimientosAsync se consulta antes). Si de todos modos
            // hubiera historial, las FK Restrict de EntradasInventario,
            // SalidasInventario y SolicitudesPrestamo rechazan el DELETE en la base.
            context.Articulos.Remove(articulo);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsConflictoDeRestriccion(ex))
            {
                // La carrera que el chequeo previo no puede cerrar: entre el
                // TieneMovimientosAsync del servicio y este DELETE alguien registró un
                // movimiento del artículo. La base lo frena (no se pierde nada), pero
                // sin traducir salía como DbUpdateException y el servicio la envolvía
                // en "Error al eliminar el artículo", sin decir por qué.
                //
                // ValidationException y no DuplicateException: es la misma condición
                // de negocio que ya valida el servicio antes de llamar acá, y su
                // filtro de excepciones la deja pasar sin envolverla.
                throw new ValidationException(
                    "No se puede eliminar: el artículo tiene movimientos registrados.");
            }
        }

        // ------------------------------------------------------------------
        // Movimientos que mueven stock
        //
        // Los tres métodos de esta sección comparten la misma forma: UN solo
        // contexto pedido a la factory y UNA transacción explícita que abarca todas
        // las escrituras del movimiento.
        //
        // La transacción explícita hace falta y no alcanza con "un solo
        // SaveChangesAsync": ExecuteUpdateAsync no pasa por el change tracker,
        // ejecuta su UPDATE en el acto y en su propia sentencia. Lo que lo une al
        // resto de las escrituras es la transacción abierta sobre el contexto, en la
        // que se enrola igual que SaveChanges.
        //
        // Se mantiene ExecuteUpdateAsync (en vez de mover el stock con una entidad
        // rastreada) porque genera UPDATE ... SET StockActual = StockActual ± @n, que
        // la base resuelve de forma atómica. Leer-modificar-guardar dejaría que dos
        // movimientos simultáneos del mismo artículo leyeran el mismo valor inicial y
        // uno pisara al otro (lost update).
        //
        // Si algo falla antes del Commit, el using de la transacción hace rollback al
        // liberarse y no queda ninguna escritura a medias.
        // ------------------------------------------------------------------

        public async Task RegistrarEntradaConStockAsync(EntradaInventario entrada, Articulo? articuloNuevo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaccion = await context.Database.BeginTransactionAsync();

            if (articuloNuevo is not null)
            {
                context.Articulos.Add(articuloNuevo);

                try
                {
                    // SaveChanges propio y no diferido: hace falta el Id generado para
                    // poder colgarle la entrada. Sigue dentro de la transacción, así
                    // que el artículo no queda creado si la entrada falla después.
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
                {
                    // Choque contra UX_Articulos_Nombre: otro usuario creó el mismo
                    // artículo entre la búsqueda por nombre del servicio y este
                    // INSERT. La transacción revierte y el mensaje explica qué hacer.
                    throw new DuplicateException(
                        $"Otro usuario acaba de crear el artículo '{articuloNuevo.Nombre}'. " +
                        "Volvé a registrar la entrada para que se sume a ese artículo.");
                }

                entrada.ArticuloId = articuloNuevo.Id;
            }

            context.EntradasInventario.Add(entrada);
            await context.SaveChangesAsync();

            // Variables locales y no entrada.ArticuloId / entrada.Cantidad dentro del
            // árbol de expresión: así se capturan como parámetros SQL simples y no se
            // intenta traducir un acceso a la entidad rastreada.
            var articuloId = entrada.ArticuloId;
            var cantidad = entrada.Cantidad;

            var filasAfectadas = await context.Articulos
                .Where(a => a.Id == articuloId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.StockActual, a => a.StockActual + cantidad));

            // La FK de EntradasInventario ya garantiza que el artículo existe, así que
            // cero filas sería una inconsistencia del esquema. Se falla y se revierte
            // en vez de dar la entrada por registrada sin sumar el stock.
            if (filasAfectadas == 0)
            {
                throw new InvalidOperationException(
                    "No se pudo actualizar el stock: el artículo de la entrada no existe.");
            }

            await transaccion.CommitAsync();
        }

        public async Task RegistrarSalidaConStockAsync(SalidaInventario salida)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaccion = await context.Database.BeginTransactionAsync();

            context.SalidasInventario.Add(salida);
            await context.SaveChangesAsync();

            await DescontarStockAsync(context, salida.ArticuloId, salida.Cantidad);

            await transaccion.CommitAsync();
        }

        public async Task AprobarPrestamoConStockAsync(SolicitudPrestamo solicitud, SalidaInventario salida)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaccion = await context.Database.BeginTransactionAsync();

            var existente = await context.SolicitudesPrestamo
                .FirstOrDefaultAsync(s => s.Id == solicitud.Id)
                ?? throw new NotFoundException("La solicitud no existe.");

            // Se relee el estado DENTRO de la transacción y no se confía en el que
            // validó el servicio: entre aquella lectura y esta pudo resolverse la
            // solicitud. La red definitiva contra la doble aprobación sigue siendo
            // UX_SalidasInventario_SolicitudPrestamo (índice único filtrado), que
            // ahora además revierte la salida y el descuento en vez de dejarlos.
            if (existente.Estado != EstadoSolicitudPrestamo.Pendiente)
                throw new ValidationException("La solicitud ya fue resuelta.");

            existente.Estado = solicitud.Estado;
            existente.MotivoRechazo = solicitud.MotivoRechazo;

            context.SalidasInventario.Add(salida);

            try
            {
                // Un solo SaveChanges para el cambio de estado y la salida: ambas son
                // escrituras rastreadas y viajan juntas.
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                throw new ValidationException(
                    "La solicitud acaba de ser aprobada por otro usuario.");
            }

            await DescontarStockAsync(context, salida.ArticuloId, salida.Cantidad);

            await transaccion.CommitAsync();
        }

        // La condición "hay stock suficiente" viaja dentro del WHERE del UPDATE en vez
        // de evaluarse antes: así el chequeo y el descuento son la misma operación y
        // dos salidas simultáneas no pueden dejar el stock negativo. El CHECK
        // CK_Articulos_StockActual_NoNegativo es la última red de todos modos.
        //
        // Recibe el contexto por parámetro (y no lo pide a la factory) para escribir en
        // la MISMA transacción que la salida que lo invoca. Pedir otro contexto acá lo
        // dejaría fuera del rollback, que es justo el error que se está corrigiendo.
        private static async Task DescontarStockAsync(SigacDbContext context, int articuloId, int cantidad)
        {
            var filasAfectadas = await context.Articulos
                .Where(a => a.Id == articuloId && a.StockActual >= cantidad)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.StockActual, a => a.StockActual - cantidad));

            // Cero filas significa que el artículo no existe o que el stock ya no
            // alcanza, aunque el servicio lo hubiera verificado un instante antes.
            // Se lanza ValidationException y no InvalidOperationException porque es una
            // situación esperable de concurrencia, no un fallo del sistema: el servicio
            // la deja pasar sin envolverla y el usuario ve el motivo real.
            //
            // Al lanzarse antes del Commit, la salida insertada en esta misma
            // transacción se revierte. Antes ya estaba confirmada y quedaba un
            // movimiento registrado sin su descuento.
            if (filasAfectadas == 0)
            {
                throw new ValidationException(
                    "El stock disponible cambió mientras se registraba el movimiento y ya no alcanza. " +
                    "Volvé a intentarlo.");
            }
        }

        private static bool EsViolacionDeUnicidad(DbUpdateException ex) =>
            ex.InnerException is SqlException sql &&
            (sql.Number == ErrorSqlRestriccionUnica || sql.Number == ErrorSqlIndiceUnico);

        private static bool EsConflictoDeRestriccion(DbUpdateException ex) =>
            ex.InnerException is SqlException sql &&
            sql.Number == ErrorSqlConflictoRestriccion;

        // Arma el mensaje de duplicado según CUÁL índice único rechazó la escritura.
        // SQL Server nombra el índice violado dentro del texto del error, así que
        // alcanza con buscarlo ahí; si no se lo puede identificar (otro índice, o un
        // formato de mensaje inesperado) se cae al nombre, que es el caso frecuente
        // por ser la clave natural del catálogo.
        private static string DescribirDuplicadoDeArticulo(DbUpdateException ex, Articulo articulo)
        {
            if (ex.InnerException is SqlException sql &&
                sql.Message.Contains(IndiceUnicoCodigoArticulo, StringComparison.OrdinalIgnoreCase))
            {
                return $"Ya existe otro artículo con el código '{articulo.Codigo}'.";
            }

            return $"Ya existe otro artículo con el nombre '{articulo.Nombre}'.";
        }

        // ------------------------------------------------------------------
        // Consultas de movimientos
        // ------------------------------------------------------------------

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
            // (AprobarPrestamoAsync / RechazarPrestamoAsync) modifica la solicitud y se
            // la devuelve al repositorio. Si viniera con el artículo colgado, el
            // guardado podría arrastrar también esa fila y pisarle el stock. El
            // servicio pide el artículo por separado cuando lo necesita.
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
            //
            // Este método queda para el RECHAZO, que es una escritura única y no mueve
            // stock. La aprobación va por AprobarPrestamoConStockAsync, que además
            // registra la salida y el descuento en la misma transacción.
            existente.Estado = solicitud.Estado;
            existente.MotivoRechazo = solicitud.MotivoRechazo;

            await context.SaveChangesAsync();
        }

        public async Task<IEnumerable<SolicitudPrestamo>> ObtenerSolicitudesAsync(EstadoSolicitudPrestamo? estado = null)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Acá sí va el Include: la lista muestra el nombre del artículo y las
            // entidades salen en AsNoTracking, así que no hay riesgo de arrastrarlas a
            // un guardado posterior.
            IQueryable<SolicitudPrestamo> consulta = context.SolicitudesPrestamo
                .AsNoTracking()
                .Include(s => s.Articulo);

            if (estado.HasValue)
            {
                // Variable local para que se capture como parámetro SQL. La columna
                // guarda el enum como texto (HasConversion<string>), y EF Core aplica
                // ese mismo conversor a la comparación, así que el WHERE viaja como
                // [Estado] = 'Pendiente' y puede hacer seek sobre
                // IX_SolicitudesPrestamo_Estado.
                var estadoFiltro = estado.Value;
                consulta = consulta.Where(s => s.Estado == estadoFiltro);
            }

            return await consulta
                .OrderByDescending(s => s.Fecha)
                .ThenByDescending(s => s.Id)
                .ToListAsync();
        }
    }
}
