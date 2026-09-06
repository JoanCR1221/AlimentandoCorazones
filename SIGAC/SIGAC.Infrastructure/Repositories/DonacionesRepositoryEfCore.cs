using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación real del repositorio con EF Core sobre SQL Server.
    // Respeta el contrato de IDonacionesRepository sin cambiar su firma.
    //
    // Ninguna escritura de este repositorio toca StockActual, a diferencia de las
    // operaciones compuestas de InventarioRepositoryEfCore: Inventario sigue siendo
    // el dueño del stock. Acá se guarda el "quién y por qué" de cada donación; el
    // movimiento que ajusta las existencias lo pide el servicio a IInventarioService.
    public class DonacionesRepositoryEfCore : IDonacionesRepository
    {
        // Factory y no un DbContext inyectado: en Blazor Server el scope dura toda
        // la sesión, así que un contexto compartido queda expuesto a que dos
        // operaciones lo usen a la vez (por ejemplo, el historial recargando
        // mientras se registra una entrega), y DbContext no tolera eso. Cada método
        // pide su propio contexto de corta vida.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public DonacionesRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ------------------------------------------------------------------
        // Escrituras
        // ------------------------------------------------------------------

        public async Task AgregarDonacionDineroAsync(DonacionDinero donacion)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.DonacionesDinero.Add(donacion);
            await context.SaveChangesAsync();
        }

        public async Task AgregarDonacionEspecieAsync(DonacionEspecie donacion)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Un solo Add para la cabecera y un solo SaveChanges para todo: al
            // agregar la cabecera, EF Core recorre su colección Detalles y marca cada
            // línea como Added también (rastreo en cascada de las entidades
            // alcanzables). Emite el INSERT de la cabecera, toma el Id generado y lo
            // usa en los INSERT de las líneas, todo dentro de la transacción implícita
            // de SaveChanges.
            //
            // Por eso no hace falta una transacción explícita como en las operaciones
            // de Inventario que mueven stock: aquellas combinan SaveChanges con
            // ExecuteUpdateAsync, que no pasa por el change tracker y necesita algo
            // que lo enrole. Acá todas las escrituras son rastreadas y viajan juntas.
            //
            // Guardar la cabecera por separado para obtener su Id y después las
            // líneas dejaría, si el segundo guardado falla, una donación sin detalle:
            // una fila que dice que alguien donó, sin decir qué.
            context.DonacionesEspecie.Add(donacion);
            await context.SaveChangesAsync();
        }

        public async Task AgregarDonacionEntregadaAsync(DonacionEntregada donacion)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.DonacionesEntregadas.Add(donacion);
            await context.SaveChangesAsync();
        }

        // ------------------------------------------------------------------
        // Consultas de historial
        //
        // Dinero y especie se consultan por separado porque son dos tablas con forma
        // distinta (ver el comentario de IDonacionesRepository). Las dos comparten el
        // mismo tratamiento de filtros para que el historial unificado que arma el
        // servicio no muestre criterios distintos según la clase de donación.
        //
        // Las fechas se normalizan fuera de la expresión y el límite superior es
        // "menor que el día siguiente", igual que en ObtenerEntradasAsync de
        // Inventario: si el .Date quedara del lado de la columna, EF traduciría
        // CONVERT(date, [d].[Fecha]) y el índice de Fecha dejaría de poder usarse; y
        // como la columna es datetime2 y guarda la hora, comparar contra la
        // medianoche del último día dejaría fuera una donación de las 14:30.
        // ------------------------------------------------------------------

        public async Task<IEnumerable<DonacionDinero>> ObtenerDonacionesDineroAsync(FiltrosHistorialDonacionDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Include trae el donante en el mismo viaje: sin él la navegación llega
            // en null y el historial mostraría el nombre del donante vacío.
            IQueryable<DonacionDinero> consulta = context.DonacionesDinero
                .AsNoTracking()
                .Include(d => d.Donante);

            if (filtros.DonanteId.HasValue)
            {
                // Variable local y no filtros.DonanteId.Value dentro del árbol de
                // expresión: así se captura como parámetro SQL simple. Hace seek
                // sobre IX_DonacionesDinero_Donante_Fecha.
                var donanteId = filtros.DonanteId.Value;
                consulta = consulta.Where(d => d.DonanteId == donanteId);
            }

            if (filtros.FechaDesde.HasValue)
            {
                var inicio = filtros.FechaDesde.Value.Date;
                consulta = consulta.Where(d => d.Fecha >= inicio);
            }

            if (filtros.FechaHasta.HasValue)
            {
                var finExclusivo = filtros.FechaHasta.Value.Date.AddDays(1);
                consulta = consulta.Where(d => d.Fecha < finExclusivo);
            }

            // TipoDonacion no se filtra acá: no es una columna de esta tabla sino la
            // etiqueta que distingue una consulta de la otra. Es el servicio el que,
            // según ese filtro, decide si llama a este método, al de especie o a los
            // dos.

            // El orden se fija en SQL para que el resultado sea estable entre
            // llamadas con los mismos filtros. El desempate por Id importa más que en
            // Inventario: el servicio mezcla estas filas con las de especie, y sin un
            // orden total dos donaciones de la misma fecha podrían intercambiarse.
            return await consulta
                .OrderByDescending(d => d.Fecha)
                .ThenByDescending(d => d.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<DonacionEspecie>> ObtenerDonacionesEspecieAsync(FiltrosHistorialDonacionDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Dos Include: el donante para el nombre y los detalles para armar la
            // descripción de la fila. Sin el de Detalles la colección llega vacía (no
            // hay lazy loading configurado en el proyecto) y el historial mostraría
            // donaciones en especie sin decir qué se donó.
            IQueryable<DonacionEspecie> consulta = context.DonacionesEspecie
                .AsNoTracking()
                .Include(d => d.Donante)
                .Include(d => d.Detalles);

            if (filtros.DonanteId.HasValue)
            {
                var donanteId = filtros.DonanteId.Value;
                consulta = consulta.Where(d => d.DonanteId == donanteId);
            }

            // Mismo tratamiento de fechas que en las donaciones de dinero: el
            // historial unificado consulta las dos tablas con los mismos criterios.
            if (filtros.FechaDesde.HasValue)
            {
                var inicio = filtros.FechaDesde.Value.Date;
                consulta = consulta.Where(d => d.Fecha >= inicio);
            }

            if (filtros.FechaHasta.HasValue)
            {
                var finExclusivo = filtros.FechaHasta.Value.Date.AddDays(1);
                consulta = consulta.Where(d => d.Fecha < finExclusivo);
            }

            return await consulta
                .OrderByDescending(d => d.Fecha)
                .ThenByDescending(d => d.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<DonacionEntregada>> ObtenerEntregasAsync(FiltrosHistorialEntregaDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Articulo para el nombre y la unidad de medida de la fila; Beneficiario
            // para el nombre del destinatario cuando la entrega fue a una persona
            // registrada. Cuando fue a una comunidad, la navegación llega en null
            // (BeneficiarioId es NULL) y el nombre sale de ComunidadDestinataria, que
            // es una columna de la propia entrega y no necesita Include.
            IQueryable<DonacionEntregada> consulta = context.DonacionesEntregadas
                .AsNoTracking()
                .Include(d => d.Articulo)
                .Include(d => d.Beneficiario);

            if (filtros.ArticuloId.HasValue)
            {
                // Hace seek sobre IX_DonacionesEntregadas_Articulo_Fecha.
                var articuloId = filtros.ArticuloId.Value;
                consulta = consulta.Where(d => d.ArticuloId == articuloId);
            }

            if (!string.IsNullOrWhiteSpace(filtros.TipoDestinatario))
            {
                // Igualdad exacta: el tipo sale de la lista cerrada
                // TiposDestinatarioDonacion, no se teclea.
                var tipoDestinatario = filtros.TipoDestinatario;
                consulta = consulta.Where(d => d.TipoDestinatario == tipoDestinatario);
            }

            if (filtros.BeneficiarioId.HasValue)
            {
                // Independiente del filtro anterior y no anidado dentro de él: filtrar
                // por un beneficiario concreto ya implica que el tipo es
                // "Beneficiario", así que la consulta funciona igual venga o no
                // acompañado de TipoDestinatario. Hace seek sobre
                // IX_DonacionesEntregadas_Beneficiario.
                var beneficiarioId = filtros.BeneficiarioId.Value;
                consulta = consulta.Where(d => d.BeneficiarioId == beneficiarioId);
            }

            if (filtros.FechaDesde.HasValue)
            {
                var inicio = filtros.FechaDesde.Value.Date;
                consulta = consulta.Where(d => d.Fecha >= inicio);
            }

            if (filtros.FechaHasta.HasValue)
            {
                var finExclusivo = filtros.FechaHasta.Value.Date.AddDays(1);
                consulta = consulta.Where(d => d.Fecha < finExclusivo);
            }

            return await consulta
                .OrderByDescending(d => d.Fecha)
                .ThenByDescending(d => d.Id)
                .ToListAsync();
        }
    }
}
