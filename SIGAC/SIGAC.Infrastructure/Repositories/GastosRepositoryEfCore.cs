using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación real del repositorio con EF Core sobre SQL Server, que
    // reemplaza a GastosRepositoryEnMemoria ahora que GastoOperativo está
    // configurada en SigacDbContext y tiene migración.
    // Respeta el contrato de IGastosRepository sin cambiar su firma.
    public class GastosRepositoryEfCore : IGastosRepository
    {
        // Collation acentuada-insensible (AI) para el autocompletado y la búsqueda
        // por proveedor: "Pali" encuentra a "Palí". Mismo criterio que BeneficiariosRepositoryEfCore.
        private const string ColacionSinTildes = "Latin1_General_CI_AI";

        // Factory y no un DbContext inyectado, por lo mismo que en los demás
        // repositorios del proyecto: en Blazor Server el scope dura toda la sesión
        // y un contexto compartido queda expuesto a que dos operaciones lo usen a
        // la vez (por ejemplo, el listado de gastos recargando mientras se anula
        // uno), y DbContext no tolera eso. Cada método pide su propio contexto.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public GastosRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AgregarAsync(GastoOperativo gasto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.GastosOperativos.Add(gasto);
            await context.SaveChangesAsync();
        }

        public async Task<GastoOperativo?> ObtenerPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Sin AsNoTracking: el servicio usa lo que devuelve este método como
            // entidad de trabajo para editar y después llama a ActualizarAsync, que
            // abre su propio contexto. El tracking acá no sirve para eso (contextos
            // distintos), pero tampoco estorba, y mantenerlo evita sorpresas si el
            // servicio empieza a apoyarse en la identidad de la instancia.
            return await context.GastosOperativos
                .FirstOrDefaultAsync(g => g.Id == id);
        }

        public async Task ActualizarAsync(GastoOperativo gasto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existente = await context.GastosOperativos
                .FirstOrDefaultAsync(g => g.Id == gasto.Id);

            if (existente is null)
                return;

            // Campo por campo y no Update(), por el mismo motivo que en
            // ActualizarDonanteAsync y ActualizarArticuloAsync: Update() marcaría
            // TODAS las columnas como modificadas y reescribiría también Estado y
            // MotivoAnulacion con lo que trajera la entidad desprendida.
            //
            // Esos dos se mueven por otro camino (AnularConEntradasVinculadasAsync),
            // así que una anulación hecha entre la lectura y el guardado quedaría
            // pisada: el UPDATE llevaría Estado = Activo y MotivoAnulacion = NULL, el
            // gasto se des-anularía solo y el motivo desaparecería sin rastro.
            // CK_GastosOperativos_MotivoAnulacion no puede atrapar eso, porque esa
            // combinación es válida por diseño.
            //
            // FechaRegistro queda fuera por lo mismo que en Donantes: es un dato
            // histórico que la edición no corrige.
            //
            // ATENCIÓN: cada campo editable que se agregue a GastoOperativo tiene que
            // sumarse a esta lista. Si falta, la edición lo descarta EN SILENCIO: la
            // pantalla dice "actualizado", la bitácora registra el valor nuevo y la
            // base conserva el viejo. Es exactamente lo que pasó con Moneda, que se
            // agregó a la entidad y nunca llegó acá.
            existente.TipoGastoId = gasto.TipoGastoId;
            existente.Proveedor = gasto.Proveedor;
            existente.NumeroFactura = gasto.NumeroFactura;
            existente.Fecha = gasto.Fecha;
            existente.MontoSinIva = gasto.MontoSinIva;
            existente.Iva = gasto.Iva;
            existente.Moneda = gasto.Moneda;
            existente.FormaPago = gasto.FormaPago;
            existente.NumeroCheque = gasto.NumeroCheque;
            existente.CuentaContable = gasto.CuentaContable;
            existente.DescripcionCuenta = gasto.DescripcionCuenta;
            existente.Descripcion = gasto.Descripcion;
            existente.Responsable = gasto.Responsable;

            await context.SaveChangesAsync();
        }

        // Sin paginación a propósito: el listado necesita el conjunto filtrado
        // completo para poder sumar el total acumulado (AB#2549), no solo una
        // página. El volumen de gastos operativos de la asociación no justifica
        // paginar como sí hace falta en Existencias de inventario.
        public async Task<IEnumerable<GastoOperativo>> ObtenerTodosAsync(FiltrosGastoDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // AsNoTracking: es una consulta de solo lectura que alimenta la grilla,
            // no se edita nada de lo que devuelve. Include del tipo: el listado
            // muestra su nombre y no el Id.
            var query = context.GastosOperativos
                .AsNoTracking()
                .Include(g => g.TipoGasto)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtros.Texto))
            {
                var busqueda = filtros.Texto.Trim();
                query = query.Where(g =>
                    EF.Functions.Collate(g.Proveedor, ColacionSinTildes).Contains(busqueda) ||
                    g.NumeroFactura.Contains(busqueda));
            }

            if (filtros.TipoGastoId.HasValue)
                query = query.Where(g => g.TipoGastoId == filtros.TipoGastoId.Value);

            if (filtros.GeneraInventario.HasValue)
                query = query.Where(g => g.TipoGasto!.GeneraInventario == filtros.GeneraInventario.Value);

            if (filtros.FechaDesde.HasValue)
            {
                var desde = filtros.FechaDesde.Value.Date;
                query = query.Where(g => g.Fecha >= desde);
            }

            if (filtros.FechaHasta.HasValue)
            {
                // Intervalo semiabierto [desde, hasta+1día) en vez de <= hasta.Date.
                // Fecha es datetime2, así que un gasto fechado a media mañana del
                // último día del rango es > hasta.Date y quedaría fuera del filtro.
                // Hoy el date picker manda siempre medianoche y las dos formas dan
                // lo mismo, pero esta no depende de eso.
                var hastaExclusivo = filtros.FechaHasta.Value.Date.AddDays(1);
                query = query.Where(g => g.Fecha < hastaExclusivo);
            }

            return await query
                .OrderByDescending(g => g.Fecha)
                .ToListAsync();
        }

        // Incluye proveedores de gastos anulados: el proveedor existió igual, y
        // ofrecerlo evita que se escriba de otra forma la próxima vez.
        public async Task<IReadOnlyList<string>> BuscarProveedoresAsync(string texto, int maximo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.GastosOperativos
                .AsNoTracking()
                .Where(g => EF.Functions.Collate(g.Proveedor, ColacionSinTildes).Contains(texto))
                .Select(g => g.Proveedor)
                .Distinct()
                .OrderBy(p => p)
                .Take(maximo)
                .ToListAsync();
        }

        // Abarca tablas de dos módulos (GastosOperativos, EntradasInventario y
        // Articulos) a propósito, con el mismo criterio que
        // AprobarPrestamoConStockAsync: el todo-o-nada solo se puede garantizar desde
        // una única transacción, y el dueño de esta operación es la anulación del
        // gasto. Antes eran dos pasos separados —GastosService anulaba el gasto,
        // commiteaba, y RECIÉN DESPUÉS llamaba al inventario— así que cualquier falla
        // del segundo paso dejaba el gasto anulado con sus entradas vivas y su
        // cantidad todavía sumada al stock.
        public async Task AnularConEntradasVinculadasAsync(int gastoId, string motivo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaccion = await context.Database.BeginTransactionAsync();

            var gasto = await context.GastosOperativos
                .FirstOrDefaultAsync(g => g.Id == gastoId);

            // Silencioso si no existe: quien decide si un id inexistente es error es
            // GastosService, que ya lo valida antes de llegar acá (NotFoundException).
            if (gasto is null)
                return;

            // Se relee el estado DENTRO de la transacción y no se confía en el que
            // validó el servicio, igual que en AprobarPrestamoConStockAsync: entre
            // aquella lectura y esta pudo anularse por otra vía, y anular dos veces
            // descontaría el stock de las mismas entradas dos veces.
            if (gasto.Estado == EstadoGastoOperativo.Anulado)
                throw new ValidationException("El gasto operativo ya está anulado.");

            // Estado y MotivoAnulacion se asignan juntos y viajan en el mismo
            // SaveChanges: CK_GastosOperativos_MotivoAnulacion es bidireccional, así
            // que un estado intermedio con uno solo de los dos lo violaría.
            gasto.Estado = EstadoGastoOperativo.Anulado;
            gasto.MotivoAnulacion = motivo;

            // TODAS las entradas vinculadas, no la primera. Una compra de varios
            // artículos genera una entrada POR ARTÍCULO, porque EntradaInventario
            // tiene un solo ArticuloId y una sola Cantidad. Quedarse con la primera
            // dejaba a las demás vivas, colgando de un gasto anulado y con su
            // cantidad todavía sumada al stock.
            //
            // Sin filtrar por tipo de gasto: si una entrada quedó vinculada al gasto,
            // hay que revertirla sea cual sea su tipo. Que el enlace se ofrezca solo
            // para tipos con GeneraInventario es una regla de las pantallas, no de
            // los datos (y un tipo puede dejar de generar inventario después).
            var entradas = await context.EntradasInventario
                .Where(e => e.GastoOperativoId == gastoId && !e.Anulada)
                .OrderBy(e => e.Id)
                .ToListAsync();

            // Anulada y MotivoAnulacion juntos por lo mismo que arriba, esta vez por
            // CK_EntradasInventario_MotivoAnulacion.
            foreach (var entrada in entradas)
            {
                entrada.Anulada = true;
                entrada.MotivoAnulacion = motivo;
            }

            await context.SaveChangesAsync();

            foreach (var entrada in entradas)
            {
                if (await StockArticulos.IntentarDescontarAsync(context, entrada.ArticuloId, entrada.Cantidad))
                    continue;

                // El stock no alcanza: lo que entró por esta entrada ya salió en
                // movimientos posteriores, y revertirlo dejaría el StockActual en
                // negativo (que además rechazaría el CHECK de Articulos).
                //
                // Se rechaza la anulación ENTERA y la transacción revierte también el
                // gasto y las entradas ya marcadas. Es preferible a dejar el gasto
                // anulado con el inventario descuadrado: para eso hay que registrar
                // primero el ajuste, que es lo que dice el mensaje.
                // El ?? cubre la otra causa por la que IntentarDescontarAsync devuelve
                // false: que el artículo no exista. Hoy es inalcanzable —ArticuloId es
                // FK obligatoria con Restrict, así que un artículo con entradas no se
                // puede borrar— pero sin esto el mensaje saldría con comillas vacías.
                var articuloDeLaEntrada = await context.Articulos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == entrada.ArticuloId);

                var nombreArticulo = articuloDeLaEntrada?.Etiqueta ?? "artículo desconocido";

                throw new ValidationException(
                    $"No se puede anular el gasto: la entrada de {entrada.Cantidad} de " +
                    $"'{nombreArticulo}' ya fue consumida por movimientos posteriores y " +
                    "revertirla dejaría el stock en negativo. Registrá primero el ajuste " +
                    "de inventario correspondiente.");
            }

            await transaccion.CommitAsync();
        }

        public async Task<IReadOnlyList<MontoPorTipoDto>> ObtenerMontoPorTipoAsync(int mesesHaciaAtras)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inicioVentana = InicioVentana(mesesHaciaAtras);

            var filas = await GastosActivosEnColones(context, inicioVentana)
                .GroupBy(g => g.TipoGasto!.Nombre)
                .Select(g => new { Tipo = g.Key, Monto = g.Sum(x => x.MontoSinIva + x.Iva) })
                .OrderByDescending(f => f.Monto)
                .ToListAsync();

            return filas.Select(f => new MontoPorTipoDto(f.Tipo, f.Monto)).ToList();
        }

        public async Task<IReadOnlyList<MontoPorFormaPagoDto>> ObtenerMontoPorFormaPagoAsync(int mesesHaciaAtras)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inicioVentana = InicioVentana(mesesHaciaAtras);

            var filas = await GastosActivosEnColones(context, inicioVentana)
                .GroupBy(g => g.FormaPago)
                .Select(g => new { Forma = g.Key, Monto = g.Sum(x => x.MontoSinIva + x.Iva) })
                .ToListAsync();

            return filas.Select(f => new MontoPorFormaPagoDto(f.Forma, f.Monto)).ToList();
        }

        public async Task<IReadOnlyList<MontoPorMesDto>> ObtenerMontoPorMesAsync(int mesesHaciaAtras)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inicioVentana = InicioVentana(mesesHaciaAtras);

            // Tipo anónimo y no el record directo: EF Core no traduce un GroupBy +
            // Select a un constructor posicional seguido de OrderBy (ver
            // BeneficiariosRepositoryEfCore.ObtenerAltasPorMesAsync).
            var filas = await GastosActivosEnColones(context, inicioVentana)
                .GroupBy(g => new { g.Fecha.Year, g.Fecha.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Monto = g.Sum(x => x.MontoSinIva + x.Iva) })
                .OrderBy(f => f.Year).ThenBy(f => f.Month)
                .ToListAsync();

            return filas.Select(f => new MontoPorMesDto(f.Year, f.Month, f.Monto)).ToList();
        }

        public async Task<IReadOnlyList<ConteoPorMesDto>> ObtenerCantidadPorMesAsync(int mesesHaciaAtras)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inicioVentana = InicioVentana(mesesHaciaAtras);

            // Todas las monedas: es un conteo de gastos, no una suma de montos.
            var filas = await context.GastosOperativos
                .AsNoTracking()
                .Where(g => g.Estado == EstadoGastoOperativo.Activo && g.Fecha >= inicioVentana)
                .GroupBy(g => new { g.Fecha.Year, g.Fecha.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Cantidad = g.Count() })
                .OrderBy(f => f.Year).ThenBy(f => f.Month)
                .ToListAsync();

            return filas.Select(f => new ConteoPorMesDto(f.Year, f.Month, f.Cantidad)).ToList();
        }

        public async Task<IReadOnlyList<MontoPorProveedorDto>> ObtenerTopProveedoresAsync(int mesesHaciaAtras, int maximo)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inicioVentana = InicioVentana(mesesHaciaAtras);

            var filas = await GastosActivosEnColones(context, inicioVentana)
                .GroupBy(g => g.Proveedor)
                .Select(g => new { Proveedor = g.Key, Monto = g.Sum(x => x.MontoSinIva + x.Iva) })
                .OrderByDescending(f => f.Monto)
                .Take(maximo)
                .ToListAsync();

            return filas.Select(f => new MontoPorProveedorDto(f.Proveedor, f.Monto)).ToList();
        }

        public async Task<(int Activos, int Anulados)> ObtenerConteoPorEstadoAsync(int mesesHaciaAtras)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var inicioVentana = InicioVentana(mesesHaciaAtras);

            // Los dos estados a propósito, a diferencia del resto de métodos de este
            // panorama: acá el objetivo es contar cuántos gastos se anularon, no
            // descartarlos.
            var resumen = await context.GastosOperativos
                .AsNoTracking()
                .Where(g => g.Fecha >= inicioVentana)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Activos = g.Count(x => x.Estado == EstadoGastoOperativo.Activo),
                    Anulados = g.Count(x => x.Estado == EstadoGastoOperativo.Anulado)
                })
                .FirstOrDefaultAsync();

            return (resumen?.Activos ?? 0, resumen?.Anulados ?? 0);
        }

        // Base común de las consultas de monto del panorama: solo gastos activos
        // y en colones, de la ventana de meses indicada. Solo colones porque sumar
        // monedas distintas no tiene sentido (ver ResumenGastosDto).
        private static IQueryable<GastoOperativo> GastosActivosEnColones(SigacDbContext context, DateTime inicioVentana) =>
            context.GastosOperativos
                .AsNoTracking()
                .Where(g => g.Estado == EstadoGastoOperativo.Activo
                    && g.Moneda == TiposMoneda.Colones
                    && g.Fecha >= inicioVentana);

        // Primer día del mes que queda mesesHaciaAtras meses atrás, contando el mes
        // actual como el primero. Mismo criterio que BeneficiariosRepositoryEfCore
        // y AsistenciaRepositoryEfCore.
        private static DateTime InicioVentana(int mesesHaciaAtras)
        {
            var inicioMesActual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return inicioMesActual.AddMonths(-(mesesHaciaAtras - 1));
        }
    }
}
