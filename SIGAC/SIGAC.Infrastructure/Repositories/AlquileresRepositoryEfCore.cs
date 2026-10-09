using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación EF Core de los alquileres. Mismo patrón que el resto: cada
    // método abre su propio DbContext desde el factory.
    public class AlquileresRepositoryEfCore : IAlquileresRepository
    {
        // Cuánto espera un registro a que termine otro del MISMO día antes de
        // rendirse. Registrar un alquiler tarda milisegundos: si se llega a este
        // tope, algo anda mal y es mejor fallar que dejar la pantalla colgada.
        private const int EsperaBloqueoDiaMs = 10_000;

        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public AlquileresRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AgregarAlquilerAsync(AlquilerEspacio alquiler)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await using var transaccion = await context.Database.BeginTransactionAsync();

            // Un choque de horario no se puede expresar con un índice único (es un
            // traslape de rangos, no un valor repetido), así que la condición de
            // carrera entre el chequeo del servicio y este INSERT se cierra con un
            // bloqueo de aplicación por DÍA: dos registros del mismo día se hacen uno
            // detrás del otro, y el segundo ve el alquiler del primero. Registros de
            // días distintos no se esperan entre sí. El bloqueo se libera solo al
            // terminar la transacción (@LockOwner = 'Transaction').
            var recurso = $"SIGAC.Alquileres.{alquiler.Fecha:yyyyMMdd}";

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                DECLARE @resultado int;
                EXEC @resultado = sp_getapplock
                    @Resource = {recurso},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = {EsperaBloqueoDiaMs};
                IF @resultado < 0
                    THROW 51000, 'No se pudo bloquear el día para registrar el alquiler.', 1;");

            var espacioIds = alquiler.Espacios.Select(e => e.Id).ToList();

            if (await ConsultaChoques(context, alquiler.Fecha, alquiler.HoraInicio, alquiler.HoraFin, espacioIds).AnyAsync())
                throw new ValidationException(
                    "Otro usuario acaba de reservar uno de esos sectores en ese horario. Revise el calendario y elija otra hora.");

            // Sectores y características ya existen (el servicio los leyó en otro
            // contexto): se adjuntan como Unchanged para que Add solo inserte el
            // alquiler y las filas de las tablas intermedias, no copias de ellos.
            context.AttachRange(alquiler.Espacios);
            context.AttachRange(alquiler.Caracteristicas);
            context.AlquileresEspacio.Add(alquiler);

            await context.SaveChangesAsync();
            await transaccion.CommitAsync();
        }

        public async Task<IReadOnlyList<AlquilerEspacio>> ObtenerChoquesAsync(
            DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin, IReadOnlyCollection<int> espacioIds)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await ConsultaChoques(context, fecha, horaInicio, horaFin, espacioIds)
                .AsNoTracking()
                .Include(a => a.Arrendatario)
                .Include(a => a.Espacios)
                .OrderBy(a => a.HoraInicio)
                .ToListAsync();
        }

        public async Task<AlquilerEspacio?> ObtenerPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.AlquileresEspacio
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<IReadOnlyList<AlquilerEspacio>> ObtenerHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // AsSplitQuery: con dos colecciones incluidas (sectores y
            // características), una sola consulta multiplicaría las filas por el
            // producto de las dos.
            var consulta = context.AlquileresEspacio
                .AsNoTracking()
                .Include(a => a.Arrendatario)
                .Include(a => a.Espacios)
                .Include(a => a.Caracteristicas)
                .AsSplitQuery()
                .AsQueryable();

            // Fecha es una columna "date" (sin hora), así que el límite superior
            // inclusivo es exacto: no hace falta el "menor que el día siguiente" de
            // las tablas con datetime2.
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

            // Se apoya en IX_EspaciosAlquiler_EspacioFisico.
            if (filtros.EspacioId.HasValue)
            {
                var espacioId = filtros.EspacioId.Value;
                consulta = consulta.Where(a => a.Espacios.Any(e => e.Id == espacioId));
            }

            if (filtros.Estado.HasValue)
            {
                var estado = filtros.Estado.Value;
                consulta = consulta.Where(a => a.Estado == estado);
            }

            return await consulta
                .OrderBy(a => a.Fecha)
                .ThenBy(a => a.HoraInicio)
                .ThenBy(a => a.Id)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<AlquilerPanoramaDto>> ObtenerParaPanoramaAsync(int mesesHaciaAtras)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Primer día del mes que queda mesesHaciaAtras meses atrás, contando el mes
            // actual como el primero (mismo criterio que GastosRepositoryEfCore), y el
            // primer día del mes siguiente al actual como tope: un alquiler de un mes
            // futuro todavía no es parte de lo que "ha evolucionado".
            var inicioVentana = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-(mesesHaciaAtras - 1));
            var finVentana = inicioVentana.AddMonths(mesesHaciaAtras);

            // Tipo anónimo y no el record directo: EF Core no traduce una colección
            // anidada dentro de un constructor posicional.
            var filas = await context.AlquileresEspacio
                .AsNoTracking()
                .Where(a => a.Fecha >= inicioVentana && a.Fecha < finVentana)
                .Select(a => new
                {
                    a.Fecha,
                    a.HoraInicio,
                    a.HoraFin,
                    a.Monto,
                    a.Moneda,
                    a.Estado,
                    Sectores = a.Espacios.Select(e => e.Nombre).ToList()
                })
                .ToListAsync();

            return filas
                .Select(f => new AlquilerPanoramaDto(f.Fecha, f.HoraInicio, f.HoraFin, f.Monto, f.Moneda, f.Estado, f.Sectores))
                .ToList();
        }

        public async Task<bool> CancelarAsync(int id, string motivoCancelacion)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Un solo UPDATE con la condición "sigue Reservado" en el WHERE: si otro
            // usuario lo canceló entre la lectura del servicio y acá, no se afecta
            // ninguna fila y no se pisa su motivo.
            var filas = await context.AlquileresEspacio
                .Where(a => a.Id == id && a.Estado == EstadoAlquiler.Reservado)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.Estado, EstadoAlquiler.Cancelado)
                    .SetProperty(a => a.MotivoCancelacion, motivoCancelacion));

            return filas > 0;
        }

        // Alquileres Reservados del mismo día, que se superponen con la franja y
        // comparten al menos un sector. La condición de traslape es la misma que
        // ReglasAlquiler.SeTraslapan, escrita acá para que viaje a SQL: que uno
        // termine justo cuando empieza el otro no es choque.
        private static IQueryable<AlquilerEspacio> ConsultaChoques(
            SigacDbContext context, DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin, IReadOnlyCollection<int> espacioIds)
        {
            var dia = fecha.Date;

            return context.AlquileresEspacio
                .Where(a => a.Estado == EstadoAlquiler.Reservado
                            && a.Fecha == dia
                            && a.HoraInicio < horaFin
                            && horaInicio < a.HoraFin
                            && a.Espacios.Any(e => espacioIds.Contains(e.Id)));
        }
    }
}
