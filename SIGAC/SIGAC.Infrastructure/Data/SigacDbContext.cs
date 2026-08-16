using Microsoft.EntityFrameworkCore;
using SIGAC.Domain.Entities;

namespace SIGAC.Infrastructure.Data
{
    public class SigacDbContext : DbContext
    {
        public SigacDbContext(DbContextOptions<SigacDbContext> options)
            : base(options)
        {
        }

        public DbSet<Beneficiario> Beneficiarios { get; set; }
        // public DbSet<AsistenciaComedor> AsistenciasComedor { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Beneficiario>(entity =>
            {
                entity.ToTable("Beneficiarios");

                entity.HasKey(b => b.Id);

                // Convención del proyecto: VARCHAR en lugar de NVARCHAR (IsUnicode(false)).

                entity.Property(b => b.Nombre)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(150);

                entity.Property(b => b.FechaNacimiento)
                    .IsRequired();

                entity.Property(b => b.Categoria)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(50);

                entity.Property(b => b.Telefono)
                    .IsUnicode(false)
                    .HasMaxLength(20);

                entity.Property(b => b.Direccion)
                    .IsUnicode(false)
                    .HasMaxLength(200);

                entity.Property(b => b.Estado)
                    .IsRequired();

                entity.Property(b => b.FechaRegistro)
                    .IsRequired();

                entity.Property(b => b.TipoDocumento)
                    .IsUnicode(false)
                    .HasMaxLength(50);

                entity.Property(b => b.NumIdentidad)
                    .IsUnicode(false)
                    .HasMaxLength(30);

                entity.Property(b => b.TipoDocumentoOtro)
                    .IsUnicode(false)
                    .HasMaxLength(100);

                // Índices en los campos de búsqueda frecuente.
                entity.HasIndex(b => b.Nombre);
                entity.HasIndex(b => b.Categoria);
                entity.HasIndex(b => b.Estado);
            });
        }
    }
}
