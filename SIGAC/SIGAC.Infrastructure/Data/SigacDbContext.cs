using Microsoft.EntityFrameworkCore;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Infrastructure.Data
{
    public class SigacDbContext : DbContext
    {
        public SigacDbContext(DbContextOptions<SigacDbContext> options) : base(options)
        {
        }

        public DbSet<Beneficiario> Beneficiarios { get; set; }
        public DbSet<AsistenciaComedor> AsistenciasComedor { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Beneficiario>(entity =>
            {
                entity.ToTable("Beneficiarios");

                entity.HasKey(b => b.Id);

                // Convención del proyecto: VARCHAR en lugar de NVARCHAR (IsUnicode(false)).

                entity.Property(b => b.PrimerNombre)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(ReglasBeneficiario.LongitudMaximaNombre);

                // Obligatorias a nivel de columna aunque sean opcionales para el
                // usuario: se guardan como cadena vacía para que el índice único
                // funcione (en SQL Server dos NULL no se consideran iguales).
                entity.Property(b => b.SegundoNombre)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(ReglasBeneficiario.LongitudMaximaNombre);

                entity.Property(b => b.PrimerApellido)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(ReglasBeneficiario.LongitudMaximaApellido);

                entity.Property(b => b.SegundoApellido)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(ReglasBeneficiario.LongitudMaximaApellido);

                // Propiedad de solo lectura para listados: no es una columna.
                entity.Ignore(b => b.NombreCompleto);

                entity.Property(b => b.FechaNacimiento)
                    .IsRequired();

                entity.Property(b => b.Categoria)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(50);

                // Teléfono de Costa Rica: 8 dígitos exactos, sin guiones ni espacios.
                entity.Property(b => b.Telefono)
                    .IsUnicode(false)
                    .HasMaxLength(ReglasBeneficiario.DigitosTelefono);

                entity.Property(b => b.Direccion)
                    .IsUnicode(false)
                    .HasMaxLength(ReglasBeneficiario.LongitudMaximaDireccion);

                entity.Property(b => b.Estado)
                    .IsRequired();

                entity.Property(b => b.FechaRegistro)
                    .IsRequired();

                entity.Property(b => b.TipoDocumento)
                    .IsUnicode(false)
                    .HasMaxLength(50);

                entity.Property(b => b.NumIdentidad)
                    .IsUnicode(false)
                    .HasMaxLength(ReglasBeneficiario.LongitudMaximaNumIdentidad);

                entity.Property(b => b.TipoDocumentoOtro)
                    .IsUnicode(false)
                    .HasMaxLength(ReglasBeneficiario.LongitudMaximaTipoDocumentoOtro);

                // Unicidad: no puede haber dos beneficiarios con los mismos nombres,
                // los mismos apellidos y la misma fecha de nacimiento. Respalda en
                // la BD la validación del servicio y cierra la condición de carrera
                // entre el SELECT previo y el INSERT.
                entity.HasIndex(b => new { b.PrimerNombre, b.SegundoNombre, b.PrimerApellido, b.SegundoApellido, b.FechaNacimiento })
                    .IsUnique()
                    .HasDatabaseName("UX_Beneficiarios_Nombres_Apellidos_FechaNacimiento");

                // Unicidad del número de identidad: no puede haber dos beneficiarios
                // con el mismo número, sin importar el tipo de documento. Es el
                // número solo y no la combinación con el tipo, porque un mismo
                // número cargado como cédula y como "Otro" es la misma persona
                // escrita dos veces: incluir el tipo dejaba pasar ese duplicado.
                //
                // Índice FILTRADO: los beneficiarios sin documento guardan
                // NumIdentidad en NULL y quedan fuera de la regla. Sin el filtro,
                // todas las personas indocumentadas chocarían entre sí.
                entity.HasIndex(b => b.NumIdentidad)
                    .IsUnique()
                    .HasFilter("[NumIdentidad] IS NOT NULL AND [NumIdentidad] <> ''")
                    .HasDatabaseName("UX_Beneficiarios_NumIdentidad");

                // Índices en los campos de filtro frecuente. El índice único anterior
                // ya cubre las búsquedas que empiezan por PrimerNombre.
                entity.HasIndex(b => b.Categoria);
                entity.HasIndex(b => b.Estado);
            });

            modelBuilder.Entity<AsistenciaComedor>(entity =>
            {
                // CHECK a nivel de BD: TiempoComida es un dominio cerrado. Refuerza
                // la validación de la capa de aplicación ante inserciones externas.
                entity.ToTable("AsistenciasComedor", t =>
                    t.HasCheckConstraint(
                        "CK_AsistenciasComedor_TiempoComida",
                        "[TiempoComida] IN ('Desayuno', 'Almuerzo', 'Merienda')"));

                entity.HasKey(a => a.Id);

                entity.Property(a => a.Fecha)
                    .IsRequired()
                    .HasColumnType("date");


                entity.Property(a => a.TiempoComida)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(20);

                // Relación FK con Beneficiario. Restrict evita borrar en duro un
                // beneficiario que tenga asistencias registradas (se conservan al desactivar).
                entity.HasOne(a => a.Beneficiario)
                    .WithMany()
                    .HasForeignKey(a => a.BeneficiarioId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Unicidad: un beneficiario NO puede registrarse dos veces en el
                // mismo tiempo de comida el mismo día.
                entity.HasIndex(a => new { a.BeneficiarioId, a.Fecha, a.TiempoComida })
                    .IsUnique()
                    .HasDatabaseName("UX_AsistenciasComedor_Beneficiario_Fecha_TiempoComida");

                // Índices en los campos de filtro frecuente. El índice único anterior
                // ya cubre las búsquedas que empiezan por BeneficiarioId.
                entity.HasIndex(a => a.Fecha);
                entity.HasIndex(a => a.TiempoComida);
            });
        }
    }
}
