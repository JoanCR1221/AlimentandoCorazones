using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Domain.Entities;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación real del repositorio con EF Core sobre SQL Server.
    // Respeta el contrato de IBeneficiariosRepository sin cambiar su firma.
    public class BeneficiariosRepositoryEfCore : IBeneficiariosRepository
    {
        private readonly SigacDbContext _context;

        public BeneficiariosRepositoryEfCore(SigacDbContext context)
        {
            _context = context;
        }

        public async Task AgregarAsync(Beneficiario beneficiario)
        {
            _context.Beneficiarios.Add(beneficiario);
            await _context.SaveChangesAsync();
        }

        public async Task ActualizarAsync(Beneficiario beneficiario)
        {
            // La entidad llega desprendida (ObtenerPorIdAsync usa AsNoTracking),
            // por lo que Update la adjunta y marca todos sus campos como modificados.
            _context.Beneficiarios.Update(beneficiario);
            await _context.SaveChangesAsync();
        }

        public async Task<Beneficiario?> ObtenerPorIdAsync(int id)
        {
            return await _context.Beneficiarios
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<bool> ExisteAsync(string nombre, string primerApellido, string segundoApellido, DateTime fechaNacimiento, int? idExcluir = null)
        {
            var fecha = fechaNacimiento.Date;

            // La fecha de nacimiento se filtra en SQL (son pocos candidatos) y los
            // nombres se comparan en memoria: la normalización sin tildes no tiene
            // traducción a SQL y no hay collation que la garantice en la columna.
            var candidatos = await _context.Beneficiarios
                .AsNoTracking()
                .Where(b => b.FechaNacimiento == fecha && (idExcluir == null || b.Id != idExcluir))
                .Select(b => new { b.Nombre, b.PrimerApellido, b.SegundoApellido })
                .ToListAsync();

            return candidatos.Any(c =>
                TextoNormalizador.SonEquivalentes(c.Nombre, nombre) &&
                TextoNormalizador.SonEquivalentes(c.PrimerApellido, primerApellido) &&
                TextoNormalizador.SonEquivalentes(c.SegundoApellido, segundoApellido));
        }

        public async Task<IEnumerable<Beneficiario>> ObtenerTodosAsync(FiltrosBeneficiarioDto filtros)
        {
            var query = _context.Beneficiarios.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtros.Nombre))
            {
                var busqueda = filtros.Nombre.Trim();
                query = query.Where(b =>
                    b.Nombre.Contains(busqueda) ||
                    b.PrimerApellido.Contains(busqueda) ||
                    b.SegundoApellido.Contains(busqueda));
            }

            if (!string.IsNullOrWhiteSpace(filtros.Categoria))
                query = query.Where(b => b.Categoria == filtros.Categoria);

            if (filtros.Estado.HasValue)
                query = query.Where(b => b.Estado == filtros.Estado.Value);

            return await query.ToListAsync();
        }

        public async Task CambiarEstadoAsync(int id, bool estado)
        {
            var beneficiario = await _context.Beneficiarios
                .FirstOrDefaultAsync(b => b.Id == id);

            if (beneficiario is not null)
            {
                beneficiario.Estado = estado;
                await _context.SaveChangesAsync();
            }
        }
    }
}
