using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Consultas agregadas de solo lectura para las tarjetas de resumen. Todo se
    // calcula en SQL (COUNT / SUM / GROUP BY): ningún método trae filas.
    public class ResumenRepositoryEfCore : IResumenRepository
    {
        // Factory y no un DbContext inyectado, por la misma razón que el resto de
        // repositorios: en Blazor Server el scope dura toda la sesión y un
        // contexto compartido no tolera dos operaciones a la vez.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public ResumenRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // Meses calendario con la hora local, la misma con la que se guardan las
        // fechas. Rangos semiabiertos [inicio, fin): un registro del último día del
        // mes a las 23:59 entra, y uno con fecha futura no cuenta como "este mes".
        private readonly record struct Periodos(
            DateTime Hoy,
            DateTime Manana,
            DateTime InicioMesAnterior,
            DateTime InicioMes,
            DateTime InicioMesSiguiente)
        {
            public static Periodos Actuales()
            {
                var ahora = DateTime.Now;
                var inicioMes = new DateTime(ahora.Year, ahora.Month, 1);

                return new Periodos(
                    ahora.Date,
                    ahora.Date.AddDays(1),
                    inicioMes.AddMonths(-1),
                    inicioMes,
                    inicioMes.AddMonths(1));
            }
        }

        public async Task<ResumenDonacionesDto> ObtenerResumenDonacionesAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var p = Periodos.Actuales();

            var dineroEsteMes = await context.DonacionesDinero
                .AsNoTracking()
                .Where(d => d.Fecha >= p.InicioMes && d.Fecha < p.InicioMesSiguiente)
                .GroupBy(d => d.Moneda)
                .Select(g => new MontoPorMonedaDto(g.Key, g.Sum(d => d.Monto)))
                .ToListAsync();

            var dineroEsteMesCantidad = await context.DonacionesDinero
                .AsNoTracking()
                .CountAsync(d => d.Fecha >= p.InicioMes && d.Fecha < p.InicioMesSiguiente);

            var dineroMesAnteriorCantidad = await context.DonacionesDinero
                .AsNoTracking()
                .CountAsync(d => d.Fecha >= p.InicioMesAnterior && d.Fecha < p.InicioMes);

            var especieEsteMesCantidad = await context.DonacionesEspecie
                .AsNoTracking()
                .CountAsync(d => d.Fecha >= p.InicioMes && d.Fecha < p.InicioMesSiguiente);

            var especieMesAnteriorCantidad = await context.DonacionesEspecie
                .AsNoTracking()
                .CountAsync(d => d.Fecha >= p.InicioMesAnterior && d.Fecha < p.InicioMes);

            // Unidades donadas: suma de las cantidades de cada línea. El (int?) hace
            // que SUM sobre cero filas dé null en vez de fallar.
            var articulosEsteMes = await context.DetallesDonacionEspecie
                .AsNoTracking()
                .Where(d => d.DonacionEspecie!.Fecha >= p.InicioMes && d.DonacionEspecie.Fecha < p.InicioMesSiguiente)
                .SumAsync(d => (int?)d.Cantidad) ?? 0;

            var articulosMesAnterior = await context.DetallesDonacionEspecie
                .AsNoTracking()
                .Where(d => d.DonacionEspecie!.Fecha >= p.InicioMesAnterior && d.DonacionEspecie.Fecha < p.InicioMes)
                .SumAsync(d => (int?)d.Cantidad) ?? 0;

            return new ResumenDonacionesDto(
                dineroEsteMesCantidad + especieEsteMesCantidad,
                dineroMesAnteriorCantidad + especieMesAnteriorCantidad,
                articulosEsteMes,
                articulosMesAnterior,
                dineroEsteMes);
        }

        public async Task<ResumenInventarioDto> ObtenerResumenInventarioAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var p = Periodos.Actuales();

            var articulos = await context.Articulos.AsNoTracking().CountAsync();

            // Las entradas anuladas no ingresaron nada: se descartan.
            var ingresadasEsteMes = await context.EntradasInventario
                .AsNoTracking()
                .Where(e => !e.Anulada && e.Fecha >= p.InicioMes && e.Fecha < p.InicioMesSiguiente)
                .SumAsync(e => (int?)e.Cantidad) ?? 0;

            var ingresadasMesAnterior = await context.EntradasInventario
                .AsNoTracking()
                .Where(e => !e.Anulada && e.Fecha >= p.InicioMesAnterior && e.Fecha < p.InicioMes)
                .SumAsync(e => (int?)e.Cantidad) ?? 0;

            return new ResumenInventarioDto(articulos, ingresadasEsteMes, ingresadasMesAnterior);
        }

        public async Task<ResumenAsistenciaDto> ObtenerResumenAsistenciaAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var p = Periodos.Actuales();

            var comidasHoy = await context.AsistenciasComedor
                .AsNoTracking()
                .CountAsync(a => a.Fecha >= p.Hoy && a.Fecha < p.Manana);

            var comidasEsteMes = await context.AsistenciasComedor
                .AsNoTracking()
                .CountAsync(a => a.Fecha >= p.InicioMes && a.Fecha < p.InicioMesSiguiente);

            var comidasMesAnterior = await context.AsistenciasComedor
                .AsNoTracking()
                .CountAsync(a => a.Fecha >= p.InicioMesAnterior && a.Fecha < p.InicioMes);

            var personasEsteMes = await context.AsistenciasComedor
                .AsNoTracking()
                .Where(a => a.Fecha >= p.InicioMes && a.Fecha < p.InicioMesSiguiente)
                .Select(a => a.BeneficiarioId)
                .Distinct()
                .CountAsync();

            return new ResumenAsistenciaDto(comidasHoy, comidasEsteMes, comidasMesAnterior, personasEsteMes);
        }

        public async Task<ResumenGastosDto> ObtenerResumenGastosAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var p = Periodos.Actuales();

            // Solo gastos activos: uno anulado no es un gasto.
            var activos = context.GastosOperativos
                .AsNoTracking()
                .Where(g => g.Estado == EstadoGastoOperativo.Activo);

            var totalEsteMes = await activos
                .Where(g => g.Fecha >= p.InicioMes && g.Fecha < p.InicioMesSiguiente)
                .GroupBy(g => g.Moneda)
                .Select(g => new MontoPorMonedaDto(g.Key, g.Sum(x => x.Monto)))
                .ToListAsync();

            var gastosEsteMes = await activos
                .CountAsync(g => g.Fecha >= p.InicioMes && g.Fecha < p.InicioMesSiguiente);

            var gastosMesAnterior = await activos
                .CountAsync(g => g.Fecha >= p.InicioMesAnterior && g.Fecha < p.InicioMes);

            return new ResumenGastosDto(gastosEsteMes, gastosMesAnterior, totalEsteMes);
        }

        public async Task<ResumenProyectosDto> ObtenerResumenProyectosAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var resumen = await context.ProyectosComunitarios
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new ResumenProyectosDto(
                    g.Count(x => x.Estado == EstadoProyecto.Planificado),
                    g.Count(x => x.Estado == EstadoProyecto.EnCurso),
                    g.Count(x => x.Estado == EstadoProyecto.Finalizado),
                    g.Count(x => x.Estado == EstadoProyecto.Cancelado)))
                .FirstOrDefaultAsync();

            return resumen ?? new ResumenProyectosDto(0, 0, 0, 0);
        }
    }
}
