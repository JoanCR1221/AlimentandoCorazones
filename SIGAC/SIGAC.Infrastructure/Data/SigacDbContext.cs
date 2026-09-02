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

        // Módulo de Control de Inventario
        public DbSet<Articulo> Articulos { get; set; }
        public DbSet<EntradaInventario> EntradasInventario { get; set; }
        public DbSet<SalidaInventario> SalidasInventario { get; set; }
        public DbSet<SolicitudPrestamo> SolicitudesPrestamo { get; set; }

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

                // Unicidad de documento: no puede haber dos beneficiarios con el
                // mismo tipo y número. Es la combinación y no el número solo, porque
                // cédula, DIMEX y pasaportes de distintos países tienen numeraciones
                // independientes que podrían coincidir por casualidad.
                //
                // Índice FILTRADO: los beneficiarios sin documento guardan
                // NumIdentidad en NULL y quedan fuera de la regla. Sin el filtro,
                // todas las personas indocumentadas chocarían entre sí.
                entity.HasIndex(b => new { b.TipoDocumento, b.NumIdentidad })
                    .IsUnique()
                    .HasFilter("[NumIdentidad] IS NOT NULL AND [NumIdentidad] <> ''")
                    .HasDatabaseName("UX_Beneficiarios_TipoDocumento_NumIdentidad");

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

            modelBuilder.Entity<Articulo>(entity =>
            {
                // CHECK a nivel de BD: el stock es un conteo físico, nunca puede ser
                // negativo. Respalda la validación del servicio (que ya impide sacar
                // más de lo disponible) ante inserciones o updates externos.
                entity.ToTable("Articulos", t =>
                {
                    t.HasCheckConstraint(
                        "CK_Articulos_StockActual_NoNegativo",
                        "[StockActual] >= 0");

                    t.HasCheckConstraint(
                        "CK_Articulos_StockMinimo_NoNegativo",
                        "[StockMinimo] >= 0");
                });

                entity.HasKey(a => a.Id);

                // Convención del proyecto: VARCHAR en lugar de NVARCHAR (IsUnicode(false)).

                entity.Property(a => a.Nombre)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(150);

                // Corta y opcional: es un SKU tipo "P001", no una descripción.
                entity.Property(a => a.Codigo)
                    .IsUnicode(false)
                    .HasMaxLength(50);

                entity.Property(a => a.Categoria)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(100);

                // Más corta que las demás a propósito: son etiquetas de unidad
                // ("Kilogramo", "Litro", "Unidad"), no texto libre.
                entity.Property(a => a.UnidadMedida)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(50);

                entity.Property(a => a.StockActual)
                    .IsRequired();

                entity.Property(a => a.StockMinimo)
                    .IsRequired();

                // Dónde está físicamente el artículo. Texto libre y opcional: no hay
                // una entidad Ubicacion todavía, así que no hay nada que validar
                // contra una lista cerrada.
                entity.Property(a => a.Ubicacion)
                    .IsUnicode(false)
                    .HasMaxLength(100);

                // Unicidad de nombre: el servicio busca el artículo por nombre y lo
                // crea si no existe (ObtenerArticuloPorNombreAsync en
                // RegistrarEntradaAsync), así que el nombre es la clave natural del
                // catálogo. Sin este índice, dos entradas simultáneas del mismo
                // artículo nuevo crearían dos filas y el stock quedaría partido.
                // El "sin distinguir mayúsculas" lo aporta la collation CI de SQL Server.
                entity.HasIndex(a => a.Nombre)
                    .IsUnique()
                    .HasDatabaseName("UX_Articulos_Nombre");

                // Unicidad de código, igual que NumIdentidad en Beneficiario: índice
                // FILTRADO porque Código es opcional y dos artículos sin código no
                // deben chocar entre sí (dos NULL, o dos '', no son iguales para SQL
                // Server salvo que se filtren igual que aquí).
                entity.HasIndex(a => a.Codigo)
                    .IsUnique()
                    .HasFilter("[Codigo] IS NOT NULL AND [Codigo] <> ''")
                    .HasDatabaseName("UX_Articulos_Codigo");

                // Índice en el campo de filtro frecuente del listado de existencias.
                // El índice único anterior ya cubre las búsquedas por Nombre.
                entity.HasIndex(a => a.Categoria);
            });

            modelBuilder.Entity<EntradaInventario>(entity =>
            {
                // CHECK a nivel de BD: Origen es un dominio cerrado y la cantidad de
                // una entrada siempre suma stock, nunca cero ni negativo. Refuerzan
                // la validación de la capa de aplicación ante inserciones externas.
                entity.ToTable("EntradasInventario", t =>
                {
                    t.HasCheckConstraint(
                        "CK_EntradasInventario_Origen",
                        $"[Origen] IN ('{OrigenesEntradaInventario.Donacion}', '{OrigenesEntradaInventario.Compra}')");

                    t.HasCheckConstraint(
                        "CK_EntradasInventario_Cantidad",
                        "[Cantidad] > 0");
                });

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Cantidad)
                    .IsRequired();

                // datetime2 y no "date" como en AsistenciasComedor: los movimientos se
                // ordenan entre sí y varios pueden caer el mismo día (AprobarPrestamoAsync
                // sella la salida con DateTime.Now). Sin la hora, el historial de un
                // mismo día quedaría en orden arbitrario.
                entity.Property(e => e.Fecha)
                    .IsRequired();

                entity.Property(e => e.Origen)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(20);

                entity.Property(e => e.Observaciones)
                    .IsUnicode(false)
                    .HasMaxLength(500);

                // Relación FK obligatoria con Articulo. Restrict impide borrar un
                // artículo que tenga historial de entradas: el movimiento es el
                // respaldo contable de la donación o la compra y no puede quedar huérfano.
                entity.HasOne(e => e.Articulo)
                    .WithMany()
                    .HasForeignKey(e => e.ArticuloId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                // TODO (tarea 2074): FK opcional EntradaInventario -> Donante.
                //   La entidad Donante todavía NO existe en SIGAC.Domain.Entities.
                //   Al crearla, descomentar y generar la migración AddFksDonanteYGastoOperativo.
                //   Mientras tanto DonanteId es solo una columna int NULL, sin integridad
                //   referencial: se puede guardar un id de donante que no exista.
                //
                // entity.HasOne<Donante>()
                //     .WithMany()
                //     .HasForeignKey(e => e.DonanteId)
                //     .IsRequired(false)
                //     .OnDelete(DeleteBehavior.Restrict);

                // TODO (tarea 2076): FK opcional EntradaInventario -> GastoOperativo.
                //   La entidad GastoOperativo todavía NO existe en SIGAC.Domain.Entities.
                //   Mismo tratamiento que DonanteId: por ahora es solo int NULL.
                //
                // entity.HasOne<GastoOperativo>()
                //     .WithMany()
                //     .HasForeignKey(e => e.GastoOperativoId)
                //     .IsRequired(false)
                //     .OnDelete(DeleteBehavior.Restrict);

                // Índice compuesto para el historial de movimientos, que filtra por
                // artículo y rango de fechas a la vez. Al empezar por ArticuloId, EF
                // Core lo reconoce como índice de la FK y no crea otro redundante.
                entity.HasIndex(e => new { e.ArticuloId, e.Fecha })
                    .HasDatabaseName("IX_EntradasInventario_Articulo_Fecha");

                // Índice suelto en Fecha: el historial también se consulta por rango
                // de fechas sin filtrar por artículo, y ahí el compuesto no sirve
                // (no se puede hacer seek por la segunda columna del índice).
                entity.HasIndex(e => e.Fecha);
            });

            modelBuilder.Entity<SalidaInventario>(entity =>
            {
                // CHECK a nivel de BD: TipoSalida es un dominio cerrado y una salida
                // siempre descuenta stock, nunca cero ni negativo.
                entity.ToTable("SalidasInventario", t =>
                {
                    t.HasCheckConstraint(
                        "CK_SalidasInventario_TipoSalida",
                        $"[TipoSalida] IN ('{TiposSalidaInventario.Donacion}', '{TiposSalidaInventario.Prestamo}')");

                    t.HasCheckConstraint(
                        "CK_SalidasInventario_Cantidad",
                        "[Cantidad] > 0");
                });

                entity.HasKey(s => s.Id);

                entity.Property(s => s.Cantidad)
                    .IsRequired();

                // datetime2 y no "date", por lo mismo que EntradasInventario: las
                // salidas de préstamo se sellan con DateTime.Now y varias pueden
                // caer el mismo día.
                entity.Property(s => s.Fecha)
                    .IsRequired();

                entity.Property(s => s.TipoSalida)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(20);

                // Opcional: solo las salidas por donación tienen comunidad destinataria.
                // Las de préstamo identifican su destino por la solicitud asociada.
                entity.Property(s => s.ComunidadDestinataria)
                    .IsUnicode(false)
                    .HasMaxLength(150);

                entity.Property(s => s.Observaciones)
                    .IsUnicode(false)
                    .HasMaxLength(500);

                // Relación FK obligatoria con Articulo. Restrict impide borrar un
                // artículo con historial de salidas, igual que con las entradas: sin
                // los movimientos no se puede reconstruir cómo llegó el stock a su valor.
                entity.HasOne(s => s.Articulo)
                    .WithMany()
                    .HasForeignKey(s => s.ArticuloId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                // FK REAL y opcional hacia SolicitudPrestamo (y no un int suelto):
                // el valor lo escribe AprobarPrestamoAsync a partir de una solicitud
                // que acaba de leer, así que siempre apunta a una fila existente y la
                // BD puede garantizarlo. Como int suelto se podrían colar ids
                // inexistentes sin que nada avisara.
                // Nullable porque las salidas por donación no nacen de una solicitud.
                // Restrict: la salida es el comprobante de que el préstamo se entregó,
                // no puede desaparecer al borrar la solicitud.
                //
                // Sin propiedad de navegación en la entidad: no se puede agregar una
                // (las entidades del dominio están cerradas), así que se declara con
                // HasOne<T>() sin selector, apoyada solo en la FK.
                entity.HasOne<SolicitudPrestamo>()
                    .WithMany()
                    .HasForeignKey(s => s.SolicitudPrestamoId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                // Unicidad: una solicitud aprobada genera UNA sola salida. Cierra la
                // condición de carrera de AprobarPrestamoAsync, donde dos aprobaciones
                // simultáneas de la misma solicitud pasarían las dos el chequeo de
                // "ya fue resuelta" y descontarían el stock dos veces.
                //
                // Índice FILTRADO: las salidas por donación llevan la columna en NULL
                // y quedan fuera de la regla. Sin el filtro chocarían todas entre sí
                // (en SQL Server el índice único trata dos NULL como iguales).
                entity.HasIndex(s => s.SolicitudPrestamoId)
                    .IsUnique()
                    .HasFilter("[SolicitudPrestamoId] IS NOT NULL")
                    .HasDatabaseName("UX_SalidasInventario_SolicitudPrestamo");

                // Mismos índices que EntradasInventario: el historial de movimientos
                // consulta las dos tablas con los mismos filtros.
                entity.HasIndex(s => new { s.ArticuloId, s.Fecha })
                    .HasDatabaseName("IX_SalidasInventario_Articulo_Fecha");

                entity.HasIndex(s => s.Fecha);
            });

            modelBuilder.Entity<SolicitudPrestamo>(entity =>
            {
                // CHECK a nivel de BD: Estado es un dominio cerrado (el enum
                // EstadoSolicitudPrestamo) y se guarda como texto, así que sin el
                // CHECK la columna aceptaría cualquier cadena escrita desde afuera.
                entity.ToTable("SolicitudesPrestamo", t =>
                {
                    t.HasCheckConstraint(
                        "CK_SolicitudesPrestamo_Estado",
                        "[Estado] IN ('Pendiente', 'Aprobada', 'Rechazada')");

                    t.HasCheckConstraint(
                        "CK_SolicitudesPrestamo_Cantidad",
                        "[Cantidad] > 0");
                });

                entity.HasKey(s => s.Id);

                entity.Property(s => s.Cantidad)
                    .IsRequired();

                entity.Property(s => s.Fecha)
                    .IsRequired();

                entity.Property(s => s.Actividad)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(150);

                entity.Property(s => s.Solicitante)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(150);

                // El enum se guarda como texto legible ('Pendiente') y no como el int
                // del enum: la columna se entiende leyendo la tabla, el CHECK de
                // arriba puede escribirse sobre valores con significado, y agregar o
                // reordenar valores del enum no reinterpreta las filas ya guardadas.
                //
                // HasConversion<string>() es el conversor integrado de EF Core para
                // enums (EnumToStringConverter); no hace falta escribir uno a mano.
                entity.Property(s => s.Estado)
                    .IsRequired()
                    .HasConversion<string>()
                    .IsUnicode(false)
                    .HasMaxLength(20);

                // Solo se llena cuando la solicitud se rechaza.
                entity.Property(s => s.MotivoRechazo)
                    .IsUnicode(false)
                    .HasMaxLength(500);

                // Relación FK obligatoria con Articulo. Restrict impide borrar un
                // artículo que tenga solicitudes de préstamo asociadas.
                entity.HasOne(s => s.Articulo)
                    .WithMany()
                    .HasForeignKey(s => s.ArticuloId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                // Índice en el campo de filtro frecuente: la bandeja de préstamos
                // trabaja sobre las solicitudes en estado Pendiente.
                entity.HasIndex(s => s.Estado);

                entity.HasIndex(s => s.Fecha);
            });
        }
    }
}
