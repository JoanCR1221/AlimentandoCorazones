using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Catálogos de espacios físicos y características. Tablas chicas (unas pocas
    // filas cada una): se leen enteras, sin paginar.
    public class EspaciosRepositoryEfCore : IEspaciosRepository
    {
        private const int ErrorSqlRestriccionUnica = 2627;
        private const int ErrorSqlIndiceUnico = 2601;

        // Conflicto con una FK o un CHECK: acá, el Restrict de las tablas
        // intermedias al borrar algo que ya se usó en un alquiler.
        private const int ErrorSqlConflictoRestriccion = 547;

        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public EspaciosRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ---- Espacios físicos ----

        public async Task<IReadOnlyList<EspacioFisico>> ObtenerEspaciosAsync(bool soloActivos)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.EspaciosFisicos
                .AsNoTracking()
                .Where(e => !soloActivos || e.Estado)
                .OrderBy(e => e.Nombre)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<EspacioFisico>> ObtenerEspaciosPorIdsAsync(IReadOnlyCollection<int> ids)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.EspaciosFisicos
                .AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .ToListAsync();
        }

        public async Task<bool> ExisteNombreEspacioAsync(string nombre)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Igualdad directa: misma comparación que UX_EspaciosFisicos_Nombre (el
            // "sin distinguir mayúsculas" lo pone la collation CI de la base).
            return await context.EspaciosFisicos.AsNoTracking().AnyAsync(e => e.Nombre == nombre);
        }

        public async Task AgregarEspacioAsync(EspacioFisico espacio)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.EspaciosFisicos.Add(espacio);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                throw new DuplicateException($"Ya existe un espacio llamado '{espacio.Nombre}'.");
            }
        }

        public async Task<bool> ActualizarCapacidadEspacioAsync(int id, int? capacidad)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var filas = await context.EspaciosFisicos
                .Where(e => e.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Capacidad, capacidad));

            return filas > 0;
        }

        public async Task<bool> CambiarEstadoEspacioAsync(int id, bool estado)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var filas = await context.EspaciosFisicos
                .Where(e => e.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Estado, estado));

            return filas > 0;
        }

        public async Task<bool> EspacioTieneAlquileresAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Se apoya en IX_EspaciosAlquiler_EspacioFisico.
            return await context.AlquileresEspacio
                .AsNoTracking()
                .AnyAsync(a => a.Espacios.Any(e => e.Id == id));
        }

        public async Task EliminarEspacioAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var espacio = await context.EspaciosFisicos.FirstOrDefaultAsync(e => e.Id == id);
            if (espacio is null)
                return;

            context.EspaciosFisicos.Remove(espacio);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsConflictoDeRestriccion(ex))
            {
                // La carrera que el chequeo previo del servicio no cierra: entre
                // EspacioTieneAlquileresAsync y este DELETE alguien lo alquiló. La FK
                // Restrict lo frena; acá solo se traduce el mensaje.
                throw new ValidationException(
                    $"No se puede eliminar '{espacio.Nombre}': ya se usó en un alquiler. Desactívelo en su lugar.");
            }
        }

        // ---- Características ----

        public async Task<IReadOnlyList<CaracteristicaEspacio>> ObtenerCaracteristicasAsync(bool soloActivos)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.CaracteristicasEspacio
                .AsNoTracking()
                .Where(c => !soloActivos || c.Estado)
                .OrderBy(c => c.Nombre)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<CaracteristicaEspacio>> ObtenerCaracteristicasPorIdsAsync(IReadOnlyCollection<int> ids)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.CaracteristicasEspacio
                .AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();
        }

        public async Task<bool> ExisteNombreCaracteristicaAsync(string nombre)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.CaracteristicasEspacio.AsNoTracking().AnyAsync(c => c.Nombre == nombre);
        }

        public async Task AgregarCaracteristicaAsync(CaracteristicaEspacio caracteristica)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.CaracteristicasEspacio.Add(caracteristica);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                throw new DuplicateException($"Ya existe una característica llamada '{caracteristica.Nombre}'.");
            }
        }

        public async Task<bool> CambiarEstadoCaracteristicaAsync(int id, bool estado)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var filas = await context.CaracteristicasEspacio
                .Where(c => c.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, estado));

            return filas > 0;
        }

        public async Task<bool> CaracteristicaTieneAlquileresAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Se apoya en IX_CaracteristicasAlquiler_CaracteristicaEspacio.
            return await context.AlquileresEspacio
                .AsNoTracking()
                .AnyAsync(a => a.Caracteristicas.Any(c => c.Id == id));
        }

        public async Task EliminarCaracteristicaAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var caracteristica = await context.CaracteristicasEspacio.FirstOrDefaultAsync(c => c.Id == id);
            if (caracteristica is null)
                return;

            context.CaracteristicasEspacio.Remove(caracteristica);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsConflictoDeRestriccion(ex))
            {
                // Misma carrera que en EliminarEspacioAsync.
                throw new ValidationException(
                    $"No se puede eliminar '{caracteristica.Nombre}': ya se pidió en un alquiler. Desactívela en su lugar.");
            }
        }

        // ---- Horario de alquiler ----

        public async Task<HorarioAlquiler> ObtenerHorarioAsync()
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.HorariosAlquiler
                       .AsNoTracking()
                       .FirstOrDefaultAsync(h => h.Id == HorarioAlquiler.IdUnico)
                   ?? HorarioAlquiler.PorDefecto();
        }

        public async Task GuardarHorarioAsync(HorarioAlquiler horario)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var filas = await context.HorariosAlquiler
                .Where(h => h.Id == HorarioAlquiler.IdUnico)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.AperturaEntreSemana, horario.AperturaEntreSemana)
                    .SetProperty(h => h.CierreEntreSemana, horario.CierreEntreSemana)
                    .SetProperty(h => h.AperturaFinDeSemana, horario.AperturaFinDeSemana)
                    .SetProperty(h => h.CierreFinDeSemana, horario.CierreFinDeSemana));

            // La migración siembra la fila; si alguien la borró desde afuera, se
            // vuelve a crear en vez de perder el cambio en silencio.
            if (filas == 0)
            {
                horario.Id = HorarioAlquiler.IdUnico;
                context.HorariosAlquiler.Add(horario);
                await context.SaveChangesAsync();
            }
        }

        private static bool EsViolacionDeUnicidad(DbUpdateException ex) =>
            ex.InnerException is SqlException sql &&
            (sql.Number == ErrorSqlRestriccionUnica || sql.Number == ErrorSqlIndiceUnico);

        private static bool EsConflictoDeRestriccion(DbUpdateException ex) =>
            ex.InnerException is SqlException sql && sql.Number == ErrorSqlConflictoRestriccion;
    }
}
