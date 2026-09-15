using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    public class BitacoraRepositoryEfCore : IBitacoraRepository
    {
        // Factory y no DbContext inyectado, por lo mismo que en el resto de los
        // repositorios. Además, acá es imprescindible: los servicios registran en
        // bitácora desde adentro de sus propias operaciones y este contexto no
        // debe compartir transacción ni tracker con ninguna de ellas.
        private readonly IDbContextFactory<SigacDbContext> _contextFactory;

        public BitacoraRepositoryEfCore(IDbContextFactory<SigacDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task RegistrarAccionAsync(BitacoraAccion accion)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Bitacora.Add(accion);
            await context.SaveChangesAsync();
        }

        public async Task<ResultadoPaginado<BitacoraAccion>> ObtenerAccionesAsync(FiltrosBitacoraDto filtros)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var consulta = context.Bitacora.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtros.UsuarioId))
            {
                var usuarioId = filtros.UsuarioId;
                consulta = consulta.Where(b => b.UsuarioId == usuarioId);
            }

            if (!string.IsNullOrWhiteSpace(filtros.Rol))
            {
                var rol = filtros.Rol;
                consulta = consulta.Where(b => b.Rol == rol);
            }

            if (!string.IsNullOrWhiteSpace(filtros.Modulo))
            {
                var modulo = filtros.Modulo;
                consulta = consulta.Where(b => b.Modulo == modulo);
            }

            if (!string.IsNullOrWhiteSpace(filtros.Accion))
            {
                var accion = filtros.Accion;
                consulta = consulta.Where(b => b.Accion == accion);
            }

            if (filtros.FechaDesde.HasValue)
            {
                var desde = filtros.FechaDesde.Value.Date;
                consulta = consulta.Where(b => b.Fecha >= desde);
            }

            if (filtros.FechaHasta.HasValue)
            {
                // Intervalo semiabierto [desde, hasta + 1 día): Fecha lleva hora y
                // comparar con <= hasta.Date dejaría fuera todo ese último día.
                var hastaExclusivo = filtros.FechaHasta.Value.Date.AddDays(1);
                consulta = consulta.Where(b => b.Fecha < hastaExclusivo);
            }

            var total = await consulta.CountAsync();

            // Más reciente primero; Id desempata dos acciones del mismo instante
            // para que Skip/Take sea determinista.
            var elementos = await consulta
                .OrderByDescending(b => b.Fecha)
                .ThenByDescending(b => b.Id)
                .Skip(filtros.PaginaEfectiva * filtros.TamanoPaginaEfectivo)
                .Take(filtros.TamanoPaginaEfectivo)
                .ToListAsync();

            return new ResultadoPaginado<BitacoraAccion>(elementos, total);
        }
    }
}
