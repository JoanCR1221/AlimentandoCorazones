using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Asistencia;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación real del repositorio con EF Core sobre SQL Server.
    // Respeta el contrato de IAsistenciaRepository sin cambiar su firma.
    // Reemplaza a AsistenciaRepositoryEnMemoria, que perdía todo al reiniciar.
    public class AsistenciaRepositoryEfCore : IAsistenciaRepository
    {
        // Factory y no un DbContext inyectado: en Blazor Server el scope dura toda
        // la sesión, así que un contexto compartido queda expuesto a que dos
        // operaciones lo usen a la vez, y DbContext no tolera eso. Cada método
        // pide su propio contexto de corta vida.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public AsistenciaRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AgregarAsync(AsistenciaComedor asistencia)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.AsistenciasComedor.Add(asistencia);
            await context.SaveChangesAsync();
        }

        public async Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var dia = fecha.Date;

            // AnyAsync se traduce a un EXISTS: la base responde con un booleano y no
            // se materializa ninguna fila. Cubierto por UX_AsistenciasComedor_Beneficiario_Fecha_TiempoComida.
            return await context.AsistenciasComedor
                .AsNoTracking()
                .AnyAsync(a =>
                    a.BeneficiarioId == beneficiarioId &&
                    a.Fecha == dia &&
                    a.TiempoComida == tiempoComida);
        }

        public async Task<IEnumerable<AsistenciaComedor>> ObtenerAsistenciasDiariasAsync(DateTime fecha)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // La fecha se normaliza acá y no dentro de la expresión: si el .Date
            // quedara del lado de la columna, EF traduciría CONVERT(date, [a].[Fecha])
            // y el índice de Fecha dejaría de poder usarse.
            var dia = fecha.Date;

            return await context.AsistenciasComedor
                .AsNoTracking()
                .Where(a => a.Fecha == dia)
                .ToListAsync();
        }

        public async Task<IEnumerable<AsistenciaComedor>> ObtenerHistorialAsync(FiltrosAsistenciaDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Recién se ejecuta la consulta acá. El orden se fija en SQL para que el
            // resultado sea estable entre llamadas con los mismos filtros.
            return await ConstruirConsultaFiltrada(context, filtros)
                .OrderByDescending(a => a.Fecha)
                .ThenByDescending(a => a.Id)
                .ToListAsync();
        }

        // Compone los filtros sobre un IQueryable: todo viaja a la base como WHERE.
        // Nada de LINQ to Objects, o habría que traer el historial entero para
        // filtrarlo en memoria, y crece con cada día registrado.
        private static IQueryable<AsistenciaComedor> ConstruirConsultaFiltrada(SigacDbContext context, FiltrosAsistenciaDto filtros)
        {
            // Include trae el beneficiario en el mismo viaje: sin él la navegación
            // llega en null y quien lea a.Beneficiario.NombreCompleto vería vacío.
            IQueryable<AsistenciaComedor> consulta = context.AsistenciasComedor
                .AsNoTracking()
                .Include(a => a.Beneficiario);

            if (filtros.BeneficiarioId.HasValue)
            {
                var beneficiarioId = filtros.BeneficiarioId.Value;
                consulta = consulta.Where(a => a.BeneficiarioId == beneficiarioId);
            }

            if (!string.IsNullOrWhiteSpace(filtros.TiempoComida))
            {
                var tiempoComida = filtros.TiempoComida;
                consulta = consulta.Where(a => a.TiempoComida == tiempoComida);
            }

            if (filtros.FechaDesde.HasValue)
            {
                var desde = filtros.FechaDesde.Value.Date;
                consulta = consulta.Where(a => a.Fecha >= desde);
            }

            if (filtros.FechaHasta.HasValue)
            {
                var hasta = filtros.FechaHasta.Value.Date;
                consulta = consulta.Where(a => a.Fecha <= hasta);
            }

            return consulta;
        }
    }
}
