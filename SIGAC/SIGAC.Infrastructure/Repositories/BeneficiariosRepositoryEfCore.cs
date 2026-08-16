using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.Interfaces;
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

        public async Task<bool> ExisteAsync(string nombre, DateTime fechaNacimiento)
        {
            return await _context.Beneficiarios
                .AsNoTracking()
                .AnyAsync(b => b.Nombre == nombre && b.FechaNacimiento == fechaNacimiento);
        }

        public async Task<IEnumerable<Beneficiario>> ObtenerTodosAsync(FiltrosBeneficiarioDto filtros)
        {
            var query = _context.Beneficiarios.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filtros.Nombre))
                query = query.Where(b => b.Nombre.Contains(filtros.Nombre));

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
