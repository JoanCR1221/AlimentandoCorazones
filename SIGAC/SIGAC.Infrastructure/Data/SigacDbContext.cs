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

        // Módulo de Gestión de Donaciones
        public DbSet<Donante> Donantes { get; set; }
        public DbSet<DonacionDinero> DonacionesDinero { get; set; }
        public DbSet<DonacionEspecie> DonacionesEspecie { get; set; }
        public DbSet<DetalleDonacionEspecie> DetallesDonacionEspecie { get; set; }
        public DbSet<DonacionEntregada> DonacionesEntregadas { get; set; }

        // Módulo de Gastos Operativos
        public DbSet<GastoOperativo> GastosOperativos { get; set; }

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

                    // Una entrada anulada tiene que decir por qué, y una vigente no
                    // puede arrastrar el motivo de una anulación que se revirtió. La
                    // aplicación ya lo garantiza en AnularEntradaConStockAsync; esto
                    // lo sostiene ante updates externos. Mismo criterio que
                    // CK_GastosOperativos_MotivoAnulacion.
                    t.HasCheckConstraint(
                        "CK_EntradasInventario_MotivoAnulacion",
                        "([Anulada] = 1 AND [MotivoAnulacion] IS NOT NULL) OR " +
                        "([Anulada] = 0 AND [MotivoAnulacion] IS NULL)");
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

                entity.Property(e => e.Anulada)
                    .IsRequired()
                    .HasDefaultValue(false);

                entity.Property(e => e.MotivoAnulacion)
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

                // FK opcional EntradaInventario -> Donante (tarea 2074, ya activa).
                //
                // Hasta que existió la entidad Donante, DonanteId era solo una columna
                // int NULL sin integridad referencial: se podía guardar el id de un
                // donante inexistente. Ahora la BD lo garantiza.
                //
                // Nullable porque las entradas por compra no tienen donante: el
                // origen "Compra" apunta a GastoOperativoId, no acá.
                //
                // Restrict, igual que el resto de las FK del proyecto: la entrada es
                // el respaldo contable de la donación recibida, así que un donante
                // con historial de entradas no se borra en duro (se desactiva con
                // Estado, mismo criterio que en DonacionesDinero y DonacionesEspecie).
                //
                // Sin propiedad de navegación en la entidad, igual que la FK de
                // SalidasInventario hacia SolicitudPrestamo: EntradaInventario
                // pertenece al módulo de Inventario y no se le agrega una referencia
                // a un tipo de Donaciones, así que la relación se declara con
                // HasOne<T>() sin selector, apoyada solo en la columna FK.
                entity.HasOne<Donante>()
                    .WithMany()
                    .HasForeignKey(e => e.DonanteId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                // FK opcional EntradaInventario -> GastoOperativo (tarea 2076, ya activa).
                //
                // Contraparte exacta de la FK a Donante de arriba: hasta que existió la
                // entidad GastoOperativo, GastoOperativoId era solo una columna int NULL
                // sin integridad referencial. Ahora la BD garantiza que una entrada por
                // compra apunte a un gasto que existe de verdad.
                //
                // Nullable porque las entradas por donación no tienen gasto: el origen
                // "Donacion" apunta a DonanteId, no acá. Las dos FK son excluyentes en
                // la práctica, según el valor de Origen.
                //
                // Restrict, igual que el resto de las FK del proyecto: la entrada es el
                // respaldo contable de la compra, así que un gasto con entradas
                // vinculadas no se borra en duro. Se anula con Estado, y esa anulación
                // arrastra la entrada a Anulada = true (AnularEntradaVinculadaAGastoAsync).
                //
                // Sin propiedad de navegación, mismo criterio que la FK a Donante:
                // EntradaInventario pertenece al módulo de Inventario y no se le agrega
                // una referencia a un tipo de Gastos.
                entity.HasOne<GastoOperativo>()
                    .WithMany()
                    .HasForeignKey(e => e.GastoOperativoId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                // Índice compuesto para el historial de movimientos, que filtra por
                // artículo y rango de fechas a la vez. Al empezar por ArticuloId, EF
                // Core lo reconoce como índice de la FK y no crea otro redundante.
                entity.HasIndex(e => new { e.ArticuloId, e.Fecha })
                    .HasDatabaseName("IX_EntradasInventario_Articulo_Fecha");

                // Índice de la FK a Donante. Hace falta declararlo: a diferencia de
                // ArticuloId, ninguna otra clave de esta tabla empieza por DonanteId,
                // así que el compuesto de arriba no lo cubre. Sin él, EF Core crearía
                // uno automático de todos modos, pero con nombre generado; declararlo
                // acá le fija el nombre y deja explícito para qué está, igual que
                // IX_DonacionesEntregadas_Beneficiario.
                //
                // Sirve a las dos consultas que aparecen con la FK activa: "qué ha
                // donado este donante" desde su ficha, y la verificación que hace SQL
                // Server al intentar borrar un donante (Restrict tiene que recorrer
                // las entradas que lo referencian).
                entity.HasIndex(e => e.DonanteId)
                    .HasDatabaseName("IX_EntradasInventario_Donante");

                // Índice de la FK a GastoOperativo, por lo mismo que el de Donante.
                // Además de la verificación que hace Restrict al borrar, lo usa
                // ObtenerEntradaPorGastoOperativoIdAsync, que es la consulta que
                // corre cada vez que se anula un gasto de tipo compra.
                entity.HasIndex(e => e.GastoOperativoId)
                    .HasDatabaseName("IX_EntradasInventario_GastoOperativo");

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

                // Sin IsRequired(): es opcional porque una solicitud Pendiente todavía no
                // tiene resolución. Queda en NULL hasta que se aprueba o se rechaza, y
                // ese NULL es justamente lo que distingue "sin resolver" de "resuelta".
                entity.Property(s => s.FechaResolucion);

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

            // Módulo de Gestión de Donaciones

            modelBuilder.Entity<Donante>(entity =>
            {
                // CHECK a nivel de BD: TipoPersona es un dominio cerrado, igual que
                // Origen en EntradasInventario. Los valores llevan tilde y la columna
                // es VARCHAR (convención del proyecto): funciona porque la base usa
                // una collation Latin1/Spanish, la misma sobre la que ya se guardan
                // "Niño" y "Cédula nacional" en Beneficiarios.
                entity.ToTable("Donantes", t =>
                    t.HasCheckConstraint(
                        "CK_Donantes_TipoPersona",
                        $"[TipoPersona] IN ('{TiposPersonaDonante.Fisica}', '{TiposPersonaDonante.Juridica}')"));

                entity.HasKey(d => d.Id);

                // Convención del proyecto: VARCHAR en lugar de NVARCHAR (IsUnicode(false)).

                // 150 y no los 100 de ReglasBeneficiario.LongitudMaximaNombre: acá el
                // nombre completo va en UNA sola columna (un beneficiario lo reparte
                // en cuatro), y además puede ser la razón social de una empresa. Es la
                // misma longitud que SolicitudPrestamo.Solicitante, que también guarda
                // un nombre entero como texto libre.
                entity.Property(d => d.Nombre)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(150);

                // Etiqueta de dominio cerrado, no texto libre: mismos 20 caracteres
                // que Origen y TipoSalida.
                entity.Property(d => d.TipoPersona)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(20);

                // 20 y no los 8 exactos de ReglasBeneficiario.DigitosTelefono: un
                // donante puede ser una empresa (extensión) o estar en el extranjero
                // (prefijo internacional), y a diferencia de Beneficiario no hay un
                // validador que exija el formato costarricense de 8 dígitos.
                entity.Property(d => d.Telefono)
                    .IsUnicode(false)
                    .HasMaxLength(20);

                entity.Property(d => d.Correo)
                    .IsUnicode(false)
                    .HasMaxLength(150);

                entity.Property(d => d.Estado)
                    .IsRequired();

                entity.Property(d => d.FechaRegistro)
                    .IsRequired();

                // Índice NO único, a diferencia de UX_Articulos_Nombre.
                //
                // El nombre de un artículo es la clave natural del catálogo: el
                // servicio busca por nombre y crea si no existe, así que dos filas
                // con el mismo nombre son un error. El nombre de un donante es el
                // nombre de una persona: dos donantes distintos pueden llamarse
                // "José Rodríguez", y Donante no tiene número de documento con el
                // cual distinguirlos. Un índice único haría imposible registrar al
                // segundo homónimo.
                //
                // Detectar posibles duplicados al registrar es trabajo del servicio
                // (avisar, no bloquear). El índice acá existe solo para que el
                // listado busque y ordene por nombre sin recorrer la tabla entera.
                entity.HasIndex(d => d.Nombre)
                    .HasDatabaseName("IX_Donantes_Nombre");

                // Campo de filtro frecuente: el listado muestra activos por defecto,
                // igual que en Beneficiarios.
                entity.HasIndex(d => d.Estado);
            });

            modelBuilder.Entity<DonacionDinero>(entity =>
            {
                // CHECK a nivel de BD: registrar una donación de cero o negativa no
                // significa nada. Mismo criterio que CK_EntradasInventario_Cantidad.
                entity.ToTable("DonacionesDinero", t =>
                    t.HasCheckConstraint(
                        "CK_DonacionesDinero_Monto",
                        "[Monto] > 0"));

                entity.HasKey(d => d.Id);

                // Primer decimal del proyecto, así que acá queda fijada la convención
                // para los que vengan (gastos operativos, presupuestos): decimal(18,2).
                // Los 2 decimales son los céntimos; 18 dígitos sobran para cualquier
                // monto en colones. Sin precisión explícita EF Core asume ese mismo
                // decimal(18,2) PERO emite una advertencia al construir el modelo, así
                // que declararla también mantiene la compilación limpia.
                entity.Property(d => d.Monto)
                    .IsRequired()
                    .HasPrecision(18, 2);

                // datetime2 y no "date", por lo mismo que en EntradasInventario: el
                // historial ordena entre sí varias donaciones del mismo día.
                entity.Property(d => d.Fecha)
                    .IsRequired();

                entity.Property(d => d.Observaciones)
                    .IsUnicode(false)
                    .HasMaxLength(500);

                // Relación FK obligatoria con Donante. Restrict: la donación es el
                // respaldo contable del ingreso y no puede quedar huérfana, así que un
                // donante con historial no se borra en duro (se desactiva con Estado,
                // igual que un beneficiario con asistencias).
                entity.HasOne(d => d.Donante)
                    .WithMany()
                    .HasForeignKey(d => d.DonanteId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                // Compuesto para el historial por donante y rango de fechas. Al
                // empezar por DonanteId, EF Core lo reconoce como el índice de la FK y
                // no crea otro redundante (mismo patrón que en EntradasInventario).
                entity.HasIndex(d => new { d.DonanteId, d.Fecha })
                    .HasDatabaseName("IX_DonacionesDinero_Donante_Fecha");

                // Suelto en Fecha: los reportes consultan por período sin filtrar por
                // donante, y ahí el compuesto no sirve (no hay seek por su segunda columna).
                entity.HasIndex(d => d.Fecha);
            });

            modelBuilder.Entity<DonacionEspecie>(entity =>
            {
                entity.ToTable("DonacionesEspecie");

                entity.HasKey(d => d.Id);

                entity.Property(d => d.Fecha)
                    .IsRequired();

                entity.Property(d => d.Observaciones)
                    .IsUnicode(false)
                    .HasMaxLength(500);

                // Restrict por lo mismo que en DonacionDinero: el donante con historial
                // se desactiva, no se borra.
                entity.HasOne(d => d.Donante)
                    .WithMany()
                    .HasForeignKey(d => d.DonanteId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(d => new { d.DonanteId, d.Fecha })
                    .HasDatabaseName("IX_DonacionesEspecie_Donante_Fecha");

                entity.HasIndex(d => d.Fecha);
            });

            modelBuilder.Entity<DetalleDonacionEspecie>(entity =>
            {
                entity.ToTable("DetallesDonacionEspecie", t =>
                {
                    t.HasCheckConstraint(
                        "CK_DetallesDonacionEspecie_Cantidad",
                        "[Cantidad] > 0");

                    // CHECK sobre Categoria contra CategoriasArticulo. Ojo: la columna
                    // Categoria de Articulos NO tiene un CHECK equivalente (ver el
                    // comentario de CategoriasArticulo), así que a partir de acá las dos
                    // tablas dejan de tratar el mismo dominio con el mismo rigor. Vale
                    // la pena igualar Articulos en una migración aparte.
                    t.HasCheckConstraint(
                        "CK_DetallesDonacionEspecie_Categoria",
                        $"[Categoria] IN ('{CategoriasArticulo.Alimento}', '{CategoriasArticulo.Ropa}', " +
                        $"'{CategoriasArticulo.Calzado}', '{CategoriasArticulo.Equipo}')");
                });

                entity.HasKey(d => d.Id);

                // Mismas longitudes que las columnas equivalentes de Articulos: esta
                // línea se convierte en un artículo al ingresar la donación al stock, y
                // un nombre que entra acá tiene que caber allá.
                entity.Property(d => d.NombreArticulo)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(150);

                entity.Property(d => d.Cantidad)
                    .IsRequired();

                entity.Property(d => d.Categoria)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(100);

                // Sin CHECK: que la unidad sea coherente CON la categoría es una matriz
                // (UnidadesMedidaArticulo.EsValidaParaCategoria), no una lista de
                // valores permitidos. Un CHECK que la exprese sería un IN por cada
                // categoría, ilegible y con la tabla duplicada en dos lugares; esa
                // validación queda en el servicio.
                entity.Property(d => d.UnidadMedida)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(50);

                // Cascade y NO Restrict, a diferencia del resto de las FK del proyecto:
                // esta es la única relación cabecera-línea que hay. Un detalle no es un
                // registro con vida propia, es parte del documento "donación en
                // especie"; sin su cabecera no significa nada y dejarlo huérfano solo
                // produciría basura.
                //
                // Lo que protege el historial no es esta FK sino que la CABECERA no se
                // borre en duro: la donación en especie es el respaldo de una
                // EntradaInventario ya registrada. Esa regla vive en el servicio (baja
                // lógica, o simplemente no exponer borrado), no acá. Poner Restrict en
                // esta FK no la agregaría: solo obligaría a borrar las líneas a mano
                // antes de borrar la cabecera.
                entity.HasOne(d => d.DonacionEspecie)
                    .WithMany(c => c.Detalles)
                    .HasForeignKey(d => d.DonacionEspecieId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                // Índice de la FK: siempre se leen todas las líneas de una cabecera.
                entity.HasIndex(d => d.DonacionEspecieId)
                    .HasDatabaseName("IX_DetallesDonacionEspecie_DonacionEspecie");
            });

            modelBuilder.Entity<DonacionEntregada>(entity =>
            {
                entity.ToTable("DonacionesEntregadas", t =>
                {
                    t.HasCheckConstraint(
                        "CK_DonacionesEntregadas_Cantidad",
                        "[Cantidad] > 0");

                    t.HasCheckConstraint(
                        "CK_DonacionesEntregadas_TipoDestinatario",
                        $"[TipoDestinatario] IN ('{TiposDestinatarioDonacion.Beneficiario}', '{TiposDestinatarioDonacion.Comunidad}')");

                    // Exclusión mutua del destinatario. Los dos campos son NULL en la
                    // tabla porque cada entrega usa uno u otro, y ese "uno u otro" no se
                    // puede expresar con NOT NULL: hace falta este CHECK, que es lo
                    // único que impide una fila con los dos campos llenos, con los dos
                    // vacíos, o con el que no corresponde al tipo.
                    //
                    // El <> '' está por lo mismo que el filtro de UX_Articulos_Codigo:
                    // sin él, una comunidad guardada como cadena vacía pasaría el
                    // IS NOT NULL y la entrega quedaría sin destinatario real.
                    t.HasCheckConstraint(
                        "CK_DonacionesEntregadas_Destinatario",
                        $"([TipoDestinatario] = '{TiposDestinatarioDonacion.Beneficiario}' " +
                        "AND [BeneficiarioId] IS NOT NULL AND [ComunidadDestinataria] IS NULL) " +
                        $"OR ([TipoDestinatario] = '{TiposDestinatarioDonacion.Comunidad}' " +
                        "AND [ComunidadDestinataria] IS NOT NULL AND [ComunidadDestinataria] <> '' " +
                        "AND [BeneficiarioId] IS NULL)");
                });

                entity.HasKey(d => d.Id);

                entity.Property(d => d.Cantidad)
                    .IsRequired();

                entity.Property(d => d.Fecha)
                    .IsRequired();

                entity.Property(d => d.TipoDestinatario)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(20);

                // Misma longitud que SalidaInventario.ComunidadDestinataria: es el
                // mismo dato, escrito en las dos tablas.
                entity.Property(d => d.ComunidadDestinataria)
                    .IsUnicode(false)
                    .HasMaxLength(150);

                entity.Property(d => d.Observaciones)
                    .IsUnicode(false)
                    .HasMaxLength(500);

                // Relación FK obligatoria con Articulo. Restrict, igual que en
                // EntradasInventario y SalidasInventario: no se borra un artículo del
                // que quedó constancia de haberse entregado.
                entity.HasOne(d => d.Articulo)
                    .WithMany()
                    .HasForeignKey(d => d.ArticuloId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                // FK OPCIONAL con Beneficiario: solo las entregas a un beneficiario
                // registrado la usan; las entregas a una comunidad la dejan en NULL (el
                // CHECK de exclusión mutua de arriba es el que verifica que ese NULL
                // corresponda al tipo). Restrict por lo mismo que en AsistenciasComedor:
                // un beneficiario con entregas registradas se desactiva, no se borra.
                entity.HasOne(d => d.Beneficiario)
                    .WithMany()
                    .HasForeignKey(d => d.BeneficiarioId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);

                // Compuesto empezando por ArticuloId: sirve al historial por artículo y
                // hace de índice de esa FK, igual que en Entradas y Salidas.
                entity.HasIndex(d => new { d.ArticuloId, d.Fecha })
                    .HasDatabaseName("IX_DonacionesEntregadas_Articulo_Fecha");

                // Índice de la FK a Beneficiario: responde "qué se le ha entregado a
                // esta persona", que es la consulta desde la ficha del beneficiario.
                entity.HasIndex(d => d.BeneficiarioId)
                    .HasDatabaseName("IX_DonacionesEntregadas_Beneficiario");

                entity.HasIndex(d => d.Fecha);
            });

            modelBuilder.Entity<GastoOperativo>(entity =>
            {
                // CHECK a nivel de BD: Categoria es un dominio cerrado (respalda a
                // CategoriasGastoOperativo) y el monto de un gasto siempre es
                // positivo, nunca cero ni negativo. Mismo criterio que el CHECK de
                // Origen en EntradasInventario y el de Cantidad en Articulos.
                entity.ToTable("GastosOperativos", t =>
                {
                    t.HasCheckConstraint(
                        "CK_GastosOperativos_Categoria",
                        $"[Categoria] IN ('{string.Join("', '", CategoriasGastoOperativo.Todos)}')");

                    t.HasCheckConstraint(
                        "CK_GastosOperativos_Monto",
                        "[Monto] > 0");

                    // Estado también es un dominio cerrado guardado como texto, igual
                    // que en SolicitudesPrestamo: sin el CHECK la columna aceptaría
                    // cualquier cadena escrita desde afuera.
                    t.HasCheckConstraint(
                        "CK_GastosOperativos_Estado",
                        "[Estado] IN ('Activo', 'Anulado')");

                    // Un gasto anulado tiene que decir por qué, y uno activo no puede
                    // arrastrar un motivo de una anulación que se revirtió. La
                    // aplicación ya lo garantiza en AnularGastoAsync; esto lo sostiene
                    // ante updates externos.
                    t.HasCheckConstraint(
                        "CK_GastosOperativos_MotivoAnulacion",
                        "([Estado] = 'Anulado' AND [MotivoAnulacion] IS NOT NULL) OR " +
                        "([Estado] <> 'Anulado' AND [MotivoAnulacion] IS NULL)");
                });

                entity.HasKey(g => g.Id);

                // Convención del proyecto: VARCHAR en lugar de NVARCHAR (IsUnicode(false)).
                entity.Property(g => g.Categoria)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(30);

                entity.Property(g => g.Monto)
                    .IsRequired()
                    .HasPrecision(18, 2);

                // datetime2 y no "date", por lo mismo que en EntradasInventario: el
                // listado ordena entre sí varios gastos del mismo día.
                entity.Property(g => g.Fecha)
                    .IsRequired();

                entity.Property(g => g.Descripcion)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(500);

                entity.Property(g => g.Responsable)
                    .IsRequired()
                    .IsUnicode(false)
                    .HasMaxLength(150);

                // Enum como texto y no como int, igual que Estado en SolicitudesPrestamo:
                // la columna se entiende leyendo la tabla, el CHECK de arriba puede
                // escribirse sobre valores con significado, y agregar o reordenar
                // valores del enum no reinterpreta las filas ya guardadas.
                entity.Property(g => g.Estado)
                    .IsRequired()
                    .HasConversion<string>()
                    .IsUnicode(false)
                    .HasMaxLength(20);

                entity.Property(g => g.FechaRegistro)
                    .IsRequired();

                // Solo se llena cuando el gasto se anula.
                entity.Property(g => g.MotivoAnulacion)
                    .IsUnicode(false)
                    .HasMaxLength(500);

                // Compuesto (Fecha, Categoria) y no al revés: el listado siempre ordena
                // por fecha descendente y el filtro de rango de fechas es el que más se
                // usa, mientras que el de categoría es opcional (AB#2549). Con Fecha
                // primero, el índice sirve tanto al filtro de rango solo como al
                // combinado; empezando por Categoria no serviría al primero.
                entity.HasIndex(g => new { g.Fecha, g.Categoria })
                    .HasDatabaseName("IX_GastosOperativos_Fecha_Categoria");
            });
        }
    }
}
