using Microsoft.EntityFrameworkCore;

namespace SIGAC.Infrastructure.Data
{
    public class SigacDbContext : DbContext
    {
        public SigacDbContext(DbContextOptions<SigacDbContext> options)
            : base(options)
        {
        }

        // DbSet<T> de cada entidad se agregan aquí cuando existan en SIGAC.Domain
        // public DbSet<Beneficiario> Beneficiarios { get; set; }
        // public DbSet<AsistenciaComedor> AsistenciasComedor { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración Fluent API (ej. índices únicos) se agrega aquí
        }
    }
}