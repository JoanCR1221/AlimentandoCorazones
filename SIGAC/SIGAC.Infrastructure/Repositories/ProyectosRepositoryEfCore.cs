using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Proyectos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación EF Core del módulo de Proyectos, mismo patrón que
    // GastosRepositoryEfCore: cada método abre su propio DbContext a través del
    // factory, porque en Blazor Server el scope dura toda la sesión y un contexto
    // compartido queda expuesto a que dos operaciones lo usen a la vez.
    public class ProyectosRepositoryEfCore : IProyectosRepository
    {
        // Números de error de SQL Server para violación de unicidad: 2627 es una
        // restricción UNIQUE/PK y 2601 un índice único, igual que en
        // InventarioRepositoryEfCore. Se usan para traducir el choque contra
        // UX_ParticipantesProyecto_Proyecto_Beneficiario a un mensaje entendible en
        // vez de un DbUpdateException crudo.
        private const int ErrorSqlRestriccionUnica = 2627;
        private const int ErrorSqlIndiceUnico = 2601;

        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public ProyectosRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AgregarAsync(ProyectoComunitario proyecto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.ProyectosComunitarios.Add(proyecto);
            await context.SaveChangesAsync();
        }

        public async Task<ProyectoComunitario?> ObtenerPorIdAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ProyectosComunitarios
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task ActualizarAsync(ProyectoComunitario proyecto)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existente = await context.ProyectosComunitarios
                .FirstOrDefaultAsync(p => p.Id == proyecto.Id);

            if (existente is null)
                return;

            // Campo por campo y no Update(), mismo motivo que en Gastos y Donantes:
            // Update() marcaría TODAS las columnas como modificadas y reescribiría
            // también FechaRegistro/FechaFinalizacionReal con lo que trajera la
            // entidad desprendida.
            //
            // Estado SÍ se reescribe acá, a diferencia de Gastos: en este módulo
            // editar es el mecanismo para mover un proyecto entre Planificado,
            // EnCurso y Cancelado. Finalizado queda fuera de este camino porque
            // ProyectoValidator.ValidarEstadoParaEdicion ya lo rechaza antes de que
            // el servicio llegue a llamar acá.
            existente.Nombre = proyecto.Nombre;
            existente.Descripcion = proyecto.Descripcion;
            existente.FechaInicio = proyecto.FechaInicio;
            existente.FechaEstimadaFin = proyecto.FechaEstimadaFin;
            existente.Estado = proyecto.Estado;

            await context.SaveChangesAsync();
        }

        // Sin paginación a propósito, mismo criterio que Gastos: el volumen de
        // proyectos comunitarios de la asociación no lo justifica.
        public async Task<IEnumerable<ProyectoComunitario>> ObtenerTodosAsync(FiltrosProyectoDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // AsNoTracking: consulta de solo lectura para la grilla. Se incluye
            // Participantes porque ProyectoListaDto necesita TotalParticipantes.
            var query = context.ProyectosComunitarios
                .Include(p => p.Participantes)
                .AsNoTracking()
                .AsQueryable();

            if (filtros.Estado.HasValue)
                query = query.Where(p => p.Estado == filtros.Estado.Value);

            return await query
                .OrderByDescending(p => p.FechaInicio)
                .ToListAsync();
        }

        public async Task FinalizarAsync(int id)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var proyecto = await context.ProyectosComunitarios
                .FirstOrDefaultAsync(p => p.Id == id);

            // Silencioso si no existe: ProyectosService ya lo valida antes de
            // llegar acá (NotFoundException), mismo criterio que Gastos.
            if (proyecto is null)
                return;

            // Se relee el estado DENTRO de esta operación y no se confía en el que
            // validó el servicio: entre aquella lectura y esta pudo finalizarse o
            // cancelarse por otra vía.
            if (proyecto.Estado == EstadoProyecto.Finalizado)
                throw new ValidationException("El proyecto ya está finalizado.");

            if (proyecto.Estado == EstadoProyecto.Cancelado)
                throw new ValidationException("No se puede finalizar un proyecto cancelado.");

            proyecto.Estado = EstadoProyecto.Finalizado;
            proyecto.FechaFinalizacionReal = DateTime.Now;

            await context.SaveChangesAsync();
        }

        public async Task AgregarParticipanteAsync(ParticipanteProyecto participante)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.ParticipantesProyecto.Add(participante);

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
            {
                // Choque contra UX_ParticipantesProyecto_Proyecto_Beneficiario: el
                // servicio ya consultó ExisteParticipanteAsync, así que llegar acá
                // significa que otro registro del mismo beneficiario se guardó entre
                // ese chequeo y este INSERT. Se traduce a ValidationException, no a
                // DuplicateException, por el mismo motivo que la doble aprobación de
                // un préstamo: el servicio y la pantalla ya tratan ValidationException
                // como "esto es culpa del dato, no del sistema", y el mensaje sale con
                // el mismo texto que el chequeo previo para que el usuario vea siempre
                // la misma explicación gane quien gane la carrera.
                throw new ValidationException(
                    "Este beneficiario ya está registrado como participante en el proyecto.");
            }
        }

        public async Task<bool> ExisteParticipanteAsync(int proyectoId, int beneficiarioId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            return await context.ParticipantesProyecto
                .AsNoTracking()
                .AnyAsync(p => p.ProyectoId == proyectoId && p.BeneficiarioId == beneficiarioId);
        }

        private static bool EsViolacionDeUnicidad(DbUpdateException ex) =>
            ex.InnerException is SqlException sql &&
            (sql.Number == ErrorSqlRestriccionUnica || sql.Number == ErrorSqlIndiceUnico);
    }
}
