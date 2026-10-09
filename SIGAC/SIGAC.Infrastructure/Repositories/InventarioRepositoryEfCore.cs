using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.DTOs.Reportes;
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
        // choque contra UX_Articulos_Nombre_Estado y UX_SalidasInventario_SolicitudPrestamo
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
        // (El de nombre es UX_Articulos_Nombre_Estado; no hace falta una constante
        // porque es el caso por omisión de DescribirDuplicadoDeArticulo.)
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

        public async Task<IReadOnlyList<Articulo>> ObtenerArticulosPorNombreAsync(string nombre)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Igualdad directa y SIN collation explícita: es exactamente la misma
            // comparación que hace el índice único UX_Articulos_Nombre_Estado (que
            // empieza por Nombre), así que el código y la base coinciden en qué es "el
            // mismo artículo" y la consulta puede hacer seek sobre ese índice. Con
            // Collate AI se encontrarían filas que el índice considera distintas, y el
            // servicio daría por existente un artículo que al insertarse no chocaría
            // con nada.
            // El "sin distinguir mayúsculas" lo aporta la collation CI de SQL Server.
            //
            // Devuelve una lista y no una fila: el Equipo puede tener una por estado.
            return await context.Articulos
                .AsNoTracking()
                .Where(a => a.Nombre == nombre)
                .OrderBy(a => a.Id)
                .ToListAsync();
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
            existente.Estado = articulo.Estado;
            existente.UnidadMedida = articulo.UnidadMedida;
            existente.Ubicacion = articulo.Ubicacion;
            existente.StockMinimo = articulo.StockMinimo;

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                // Choque contra UX_Articulos_Nombre_Estado o UX_Articulos_Codigo al editar
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

            if (!string.IsNullOrWhiteSpace(filtros.Estado))
            {
                // Igualdad exacta, igual que la categoría: se elige de una lista.
                // Los artículos sin estado (todo lo que no es Equipo) nunca coinciden.
                var estadoFiltro = filtros.Estado;
                consulta = consulta.Where(a => a.Estado == estadoFiltro);
            }

            if (filtros.SoloStockBajo)
            {
                // Misma condición que ArticuloExistenciaDto.StockBajo en el servicio
                // (StockActual <= StockMinimo), pero evaluada en SQL: acá SÍ importa,
                // porque decide qué filas trae la página en vez de solo cómo se pinta
                // una fila ya traída.
                consulta = consulta.Where(a => a.StockActual <= a.StockMinimo);
            }

            // Consulta 1: cuántos artículos cumplen los filtros, para que la grilla
            // sepa cuántas páginas hay.
            var total = await consulta.CountAsync();

            // De menor a mayor stock cuando se pidió "solo stock bajo": el objetivo
            // de esa búsqueda es reponer, y lo más urgente (lo que queda menos) tiene
            // que aparecer primero. Sin ese filtro se mantiene el orden alfabético de
            // siempre, que es el que sirve para ubicar un artículo puntual.
            var elementos = filtros.SoloStockBajo
                ? await consulta
                    .OrderBy(a => a.StockActual)
                    .ThenBy(a => a.Nombre)
                    .ThenBy(a => a.Estado)
                    .Skip(filtros.PaginaEfectiva * filtros.TamanoPaginaEfectivo)
                    .Take(filtros.TamanoPaginaEfectivo)
                    .ToListAsync()
                : await consulta
                    .OrderBy(a => a.Nombre)
                    .ThenBy(a => a.Estado)
                    .Skip(filtros.PaginaEfectiva * filtros.TamanoPaginaEfectivo)
                    .Take(filtros.TamanoPaginaEfectivo)
                    .ToListAsync();

            return new ResultadoPaginado<Articulo>(elementos, total);
        }

        public async Task<int> ContarStockBajoAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.Articulos
                .AsNoTracking()
                .CountAsync(a => a.StockActual <= a.StockMinimo);
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
                    // Choque contra UX_Articulos_Nombre_Estado: otro usuario creó el mismo
                    // artículo entre la búsqueda por nombre del servicio y este
                    // INSERT. La transacción revierte y el mensaje explica qué hacer.
                    throw new DuplicateException(
                        $"Otro usuario acaba de crear el artículo '{articuloNuevo.Etiqueta}'. " +
                        "Vuelva a registrar la entrada para que se sume a ese artículo.");
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
            existente.FechaResolucion = solicitud.FechaResolucion;

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
            // El UPDATE condicional vive en StockArticulos, compartido con
            // GastosRepositoryEfCore. Acá solo se traduce el "no se pudo" al motivo
            // que corresponde a ESTE camino: registrar un movimiento.
            //
            // No se pudo significa que el artículo no existe o que el stock ya no
            // alcanza, aunque el servicio lo hubiera verificado un instante antes.
            // Se lanza ValidationException y no InvalidOperationException porque es una
            // situación esperable de concurrencia, no un fallo del sistema: el servicio
            // la deja pasar sin envolverla y el usuario ve el motivo real.
            //
            // Al lanzarse antes del Commit, la salida insertada en esta misma
            // transacción se revierte. Antes ya estaba confirmada y quedaba un
            // movimiento registrado sin su descuento.
            if (!await StockArticulos.IntentarDescontarAsync(context, articuloId, cantidad))
            {
                throw new ValidationException(
                    "El stock disponible cambió mientras se registraba el movimiento y ya no alcanza. " +
                    "Vuelva a intentarlo.");
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

            return $"Ya existe otro artículo con el nombre '{articulo.Etiqueta}'.";
        }

        // ------------------------------------------------------------------
        // Consultas de movimientos
        // ------------------------------------------------------------------

        public async Task<IEnumerable<EntradaInventario>> ObtenerEntradasAsync(int? articuloId, DateTime? desde, DateTime? hasta)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Include trae el artículo en el mismo viaje: sin él la navegación llega
            // en null y el historial mostraría el nombre del artículo vacío.
            //
            // El orden se fija en SQL para que el resultado sea estable entre llamadas
            // con los mismos filtros.
            return await FiltrarEntradas(context, articuloId, desde, hasta)
                .Include(e => e.Articulo)
                .OrderByDescending(e => e.Fecha)
                .ThenByDescending(e => e.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<SalidaInventario>> ObtenerSalidasAsync(int? articuloId, DateTime? desde, DateTime? hasta)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await FiltrarSalidas(context, articuloId, null, desde, hasta)
                .Include(s => s.Articulo)
                .OrderByDescending(s => s.Fecha)
                .ThenByDescending(s => s.Id)
                .ToListAsync();
        }

        // Tipo de cada clave en la consulta unificada. El valor es también el orden de
        // desempate: con la misma fecha, la entrada va antes que la salida, igual que
        // cuando el servicio concatenaba las dos listas ya ordenadas.
        private const int ClaveEntrada = 0;
        private const int ClaveSalida = 1;

        // Lo único que tienen en común las dos tablas y lo único que viaja en la unión:
        // con tres columnas del mismo tipo en ambos lados, SQL arma el UNION ALL sin
        // rellenar nada con NULL.
        private sealed class ClaveMovimiento
        {
            public int Tipo { get; set; }
            public int Id { get; set; }
            public DateTime Fecha { get; set; }
        }

        public async Task<ResultadoPaginado<ItemMovimiento>> ObtenerPaginaMovimientosAsync(FiltrosMovimientoDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Un tipo de movimiento que no existe no devuelve nada.
            if (!filtros.IncluyeEntradas && !filtros.IncluyeSalidas)
                return ResultadoPaginado<ItemMovimiento>.Vacio;

            // Paso 1: solo las claves de lo que cumple el filtro, de las tablas que
            // pide el tipo. Todavía no se ejecutó nada.
            IQueryable<ClaveMovimiento>? claves = null;

            if (filtros.IncluyeEntradas)
            {
                claves = FiltrarEntradas(context, filtros.ArticuloId, filtros.Desde, filtros.Hasta)
                    .Select(e => new ClaveMovimiento { Tipo = ClaveEntrada, Id = e.Id, Fecha = e.Fecha });
            }

            if (filtros.IncluyeSalidas)
            {
                var deSalidas = FiltrarSalidas(context, filtros.ArticuloId, filtros.TipoSalida, filtros.Desde, filtros.Hasta)
                    .Select(s => new ClaveMovimiento { Tipo = ClaveSalida, Id = s.Id, Fecha = s.Fecha });

                claves = claves is null ? deSalidas : claves.Concat(deSalidas);
            }

            // Consulta 1: cuántos cumplen el filtro (para el paginador).
            var total = await claves!.CountAsync();

            if (total == 0)
                return ResultadoPaginado<ItemMovimiento>.Vacio;

            // Consulta 2: las claves de la página. El orden es total (fecha, tipo e
            // Id): sin el desempate dos movimientos del mismo instante podrían
            // repetirse o saltarse entre una página y la siguiente. Se traduce a
            // ORDER BY ... OFFSET n ROWS FETCH NEXT m ROWS ONLY sobre el UNION ALL.
            var tamanoPagina = filtros.TamanoPaginaEfectivo;

            var pagina = await claves!
                .OrderByDescending(c => c.Fecha)
                .ThenBy(c => c.Tipo)
                .ThenByDescending(c => c.Id)
                .Skip(filtros.PaginaEfectiva * tamanoPagina)
                .Take(tamanoPagina)
                .ToListAsync();

            // Consultas 3 y 4: las filas completas, con su artículo, solo de los
            // movimientos de esta página (a lo sumo TamanoPaginaMaximo ids en cada IN).
            var idsEntradas = pagina.Where(c => c.Tipo == ClaveEntrada).Select(c => c.Id).ToList();
            var idsSalidas = pagina.Where(c => c.Tipo == ClaveSalida).Select(c => c.Id).ToList();

            var entradas = idsEntradas.Count == 0
                ? new Dictionary<int, EntradaInventario>()
                : await context.EntradasInventario
                    .AsNoTracking()
                    .Include(e => e.Articulo)
                    .Where(e => idsEntradas.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id);

            var salidas = idsSalidas.Count == 0
                ? new Dictionary<int, SalidaInventario>()
                : await context.SalidasInventario
                    .AsNoTracking()
                    .Include(s => s.Articulo)
                    .Where(s => idsSalidas.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id);

            // Se arma en el orden de las claves. Si un movimiento se borró entre la
            // consulta de las claves y la de las filas, simplemente no aparece.
            var elementos = new List<ItemMovimiento>(pagina.Count);

            foreach (var clave in pagina)
            {
                if (clave.Tipo == ClaveEntrada && entradas.TryGetValue(clave.Id, out var e))
                    elementos.Add(new ItemMovimiento(e, null));
                else if (clave.Tipo == ClaveSalida && salidas.TryGetValue(clave.Id, out var s))
                    elementos.Add(new ItemMovimiento(null, s));
            }

            return new ResultadoPaginado<ItemMovimiento>(elementos, total);
        }

        public async Task<TotalesMovimientos> ObtenerTotalesMovimientosAsync(FiltrosMovimientoDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // SumAsync sobre un int vacío devuelve 0 (COALESCE en SQL), no falla.
            var entradas = filtros.IncluyeEntradas
                ? await FiltrarEntradas(context, filtros.ArticuloId, filtros.Desde, filtros.Hasta).SumAsync(e => e.Cantidad)
                : 0;

            var salidas = filtros.IncluyeSalidas
                ? await FiltrarSalidas(context, filtros.ArticuloId, filtros.TipoSalida, filtros.Desde, filtros.Hasta).SumAsync(s => s.Cantidad)
                : 0;

            return new TotalesMovimientos(entradas, salidas);
        }

        // Los filtros de las consultas de movimientos (completas y paginada) en un solo
        // lugar, para que no puedan divergir. Todo se traduce a SQL y se aplica ANTES
        // de paginar. Sin Include ni orden: cada llamador agrega los suyos.
        private static IQueryable<EntradaInventario> FiltrarEntradas(
            SigacDbContext context, int? articuloId, DateTime? desde, DateTime? hasta)
        {
            // Sin las entradas anuladas: una compra cuyo gasto se anuló ya se revirtió
            // del stock (AnularConEntradasVinculadasAsync), así que esa entrada no
            // ingresó nada y contarla inflaba el total de entradas del historial y de
            // los reportes. Siguen en la tabla como respaldo contable. Mismo criterio
            // que ResumenRepositoryEfCore.
            IQueryable<EntradaInventario> consulta = context.EntradasInventario
                .AsNoTracking()
                .Where(e => !e.Anulada);

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

            return consulta;
        }

        // Mismo tratamiento de fechas que en las entradas: el historial de movimientos
        // consulta las dos tablas con los mismos criterios. tipoSalida null = todas.
        private static IQueryable<SalidaInventario> FiltrarSalidas(
            SigacDbContext context, int? articuloId, string? tipoSalida, DateTime? desde, DateTime? hasta)
        {
            IQueryable<SalidaInventario> consulta = context.SalidasInventario.AsNoTracking();

            if (articuloId.HasValue)
            {
                var id = articuloId.Value;
                consulta = consulta.Where(s => s.ArticuloId == id);
            }

            if (!string.IsNullOrEmpty(tipoSalida))
                consulta = consulta.Where(s => s.TipoSalida == tipoSalida);

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

            return consulta;
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
            existente.FechaResolucion = solicitud.FechaResolucion;

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

        // ------------------------------------------------------------------
        // Panorama gráfico (Reportes)
        // ------------------------------------------------------------------

        public async Task<IReadOnlyList<UnidadesPorMesYTipoDto>> ObtenerEntradasPorMesYOrigenAsync(int mesesHaciaAtras)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inicioVentana = InicioVentana(mesesHaciaAtras);

            // Tipo anónimo y no el record directo: EF Core no traduce un GroupBy +
            // Select a un constructor posicional seguido de OrderBy (ver
            // BeneficiariosRepositoryEfCore.ObtenerAltasPorMesAsync).
            var filas = await context.EntradasInventario
                .AsNoTracking()
                .Where(e => !e.Anulada && e.Fecha >= inicioVentana)
                .GroupBy(e => new { e.Fecha.Year, e.Fecha.Month, e.Origen })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Origen, Unidades = g.Sum(e => e.Cantidad) })
                .OrderBy(f => f.Year).ThenBy(f => f.Month).ThenBy(f => f.Origen)
                .ToListAsync();

            return filas.Select(f => new UnidadesPorMesYTipoDto(f.Year, f.Month, f.Origen, f.Unidades)).ToList();
        }

        public async Task<IReadOnlyList<UnidadesPorMesYTipoDto>> ObtenerSalidasPorMesYTipoAsync(int mesesHaciaAtras)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inicioVentana = InicioVentana(mesesHaciaAtras);

            var filas = await context.SalidasInventario
                .AsNoTracking()
                .Where(s => s.Fecha >= inicioVentana)
                .GroupBy(s => new { s.Fecha.Year, s.Fecha.Month, s.TipoSalida })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.TipoSalida, Unidades = g.Sum(s => s.Cantidad) })
                .OrderBy(f => f.Year).ThenBy(f => f.Month).ThenBy(f => f.TipoSalida)
                .ToListAsync();

            return filas.Select(f => new UnidadesPorMesYTipoDto(f.Year, f.Month, f.TipoSalida, f.Unidades)).ToList();
        }

        public async Task<IReadOnlyList<MovimientosPorArticuloDto>> ObtenerArticulosConMasMovimientosAsync(int mesesHaciaAtras, int maximo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inicioVentana = InicioVentana(mesesHaciaAtras);

            // Dos tablas, así que se cuenta por artículo en cada una y se suman acá
            // (un UNION en SQL no vale la pena: sale una fila por artículo que se movió
            // en la ventana). Nombre y Estado viajan en la propia agrupación (la
            // etiqueta, "Carpa (En buen estado)", se arma con Articulo.EtiquetaDe, igual
            // que el historial): antes se buscaban aparte con un IN (id1, id2, ...) con
            // un parámetro por artículo, y SQL Server rechaza una consulta de más de
            // 2 100 parámetros.
            var enEntradas = await context.EntradasInventario
                .AsNoTracking()
                .Where(e => !e.Anulada && e.Fecha >= inicioVentana)
                .GroupBy(e => new { e.ArticuloId, e.Articulo!.Nombre, e.Articulo.Estado })
                .Select(g => new { g.Key.ArticuloId, g.Key.Nombre, g.Key.Estado, Movimientos = g.Count() })
                .ToListAsync();

            var enSalidas = await context.SalidasInventario
                .AsNoTracking()
                .Where(s => s.Fecha >= inicioVentana)
                .GroupBy(s => new { s.ArticuloId, s.Articulo!.Nombre, s.Articulo.Estado })
                .Select(g => new { g.Key.ArticuloId, g.Key.Nombre, g.Key.Estado, Movimientos = g.Count() })
                .ToListAsync();

            return enEntradas.Concat(enSalidas)
                .GroupBy(f => (f.ArticuloId, f.Nombre, f.Estado))
                .Select(g => new MovimientosPorArticuloDto(Articulo.EtiquetaDe(g.Key.Nombre, g.Key.Estado), g.Sum(f => f.Movimientos)))
                .OrderByDescending(f => f.Movimientos).ThenBy(f => f.Articulo, StringComparer.CurrentCultureIgnoreCase)
                .Take(maximo)
                .ToList();
        }

        // Primer día del mes que queda mesesHaciaAtras meses atrás, contando el mes
        // actual como el primero. Mismo criterio que GastosRepositoryEfCore y
        // DonacionesRepositoryEfCore.
        private static DateTime InicioVentana(int mesesHaciaAtras)
        {
            var inicioMesActual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return inicioMesActual.AddMonths(-(mesesHaciaAtras - 1));
        }
    }
}
