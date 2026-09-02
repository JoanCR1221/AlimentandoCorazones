# Capa de datos de SIGAC

Cómo funciona la persistencia del Sistema de Gestión Alimentando Corazones: el mapeo a SQL Server con EF Core, las reglas que la base garantiza por sí sola, las migraciones y los repositorios.

## Dónde vive cada cosa

| Proyecto | Qué contiene |
|---|---|
| `SIGAC.Domain` | Entidades (`Beneficiario`, `AsistenciaComedor`, `Articulo`, ...) y reglas del negocio (`ReglasBeneficiario`, `TiemposComida`, `TiposDocumento`). No conoce EF Core ni SQL Server. |
| `SIGAC.Application` | Interfaces de repositorio (`IBeneficiariosRepository`, `IInventarioRepository`, ...), servicios y DTOs. Define **qué** se necesita de la base, no **cómo**. |
| `SIGAC.Infrastructure` | `SigacDbContext` con todo el mapeo Fluent API, las migraciones y los repositorios EF Core que implementan aquellas interfaces. |
| `SIGAC` | Presentación (Blazor Server). Solo registra los repositorios en `Program.cs`; no toca la base directamente. |

Las entidades del dominio están limpias de anotaciones: **todo el mapeo se declara en `Data/SigacDbContext.cs`** con Fluent API. Si buscás por qué una columna tiene cierto largo o cierto índice, está ahí y está comentado.

## Configuración de la conexión

La cadena vive en `SIGAC/appsettings.json` bajo la clave `SigacDb`. **Ese archivo está en `.gitignore` a propósito**: cada quien apunta a su propio servidor. Si acabás de clonar, no existe y hay que crearlo:

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "SigacDb": "Server=TU-SERVIDOR\\SQLEXPRESS;Database=SIGAC;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

> **Al desplegar en Ubuntu**, `Trusted_Connection=True` no sirve: la autenticación integrada de Windows no existe ahí. Hay que pasar a autenticación SQL (`User Id=...;Password=...`) y tomar la cadena de una variable de entorno o un gestor de secretos, no de un archivo en el repositorio.

El `DbContext` se registra como **factory**, no con `AddDbContext`:

```csharp
builder.Services.AddDbContextFactory<SigacDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("SigacDb")));
```

El motivo es Blazor Server: el scope dura todo el circuito (la sesión), no cada clic. Un `DbContext` inyectado directamente quedaría compartido entre operaciones concurrentes — la grilla recargando mientras se guarda un formulario — y `DbContext` no es seguro para eso. Con la factory, cada método de repositorio pide un contexto nuevo y de vida corta.

## Convenciones del mapeo

Se aplican a todas las tablas por igual:

- **`VARCHAR`, no `NVARCHAR`** (`IsUnicode(false)`). Los datos son español latino y entran en la página de códigos; ocupan la mitad de espacio.
- **Longitud explícita en todo texto** (`HasMaxLength`). Sin eso EF genera `varchar(max)`, que no se puede indexar.
- **Nombres de índice explícitos** con `HasDatabaseName` cuando el índice es único o compuesto: `UX_` para único, `IX_` para el resto. Los índices simples usan el nombre que genera EF.
- **`CHECK` constraints para los dominios cerrados.** Si una columna solo admite un conjunto fijo de valores, la restricción se declara en la base y no solo en el servicio: protege ante inserciones que no pasen por la aplicación.
- **`OnDelete(DeleteBehavior.Restrict)` en las FK.** Nada que tenga historial asociado se puede borrar en duro. Los beneficiarios se desactivan (`Estado = false`), no se eliminan.
- **Índices únicos que respaldan validaciones del servicio.** El servicio consulta antes de insertar, pero entre ese `SELECT` y el `INSERT` hay una ventana en la que otra operación puede meterse. El índice cierra esa condición de carrera.

## Modelo de datos

### `Beneficiarios`

| Columna | Tipo | |
|---|---|---|
| `Id` | `int` | PK, identidad |
| `PrimerNombre` | `varchar(100)` | obligatoria |
| `SegundoNombre` | `varchar(100)` | obligatoria a nivel de columna |
| `PrimerApellido` | `varchar(75)` | obligatoria |
| `SegundoApellido` | `varchar(75)` | obligatoria a nivel de columna |
| `FechaNacimiento` | `datetime2` | |
| `Categoria` | `varchar(50)` | derivada de la edad |
| `Telefono` | `varchar(8)` | opcional, 8 dígitos (Costa Rica) |
| `Direccion` | `varchar(200)` | opcional |
| `Estado` | `bit` | activo/inactivo |
| `FechaRegistro` | `datetime2` | |
| `TipoDocumento` | `varchar(50)` | opcional |
| `NumIdentidad` | `varchar(30)` | opcional |
| `TipoDocumentoOtro` | `varchar(100)` | opcional, solo si el tipo es "Otro" |

**El segundo nombre y el segundo apellido son `NOT NULL` y se guardan como cadena vacía**, aunque para el usuario sean opcionales. En SQL Server dos `NULL` no se consideran iguales, así que si fueran nulos el índice único no detectaría como duplicadas a dos personas que comparten un solo apellido.

`NombreCompleto` es una propiedad calculada y está marcada con `Ignore`: no es una columna.

Índices:

- `UX_Beneficiarios_Nombres_Apellidos_FechaNacimiento` (único) sobre los cuatro nombres y la fecha de nacimiento. No puede haber dos personas con nombres, apellidos y fecha de nacimiento idénticos.
- `UX_Beneficiarios_TipoDocumento_NumIdentidad` (único, **filtrado** por `NumIdentidad IS NOT NULL AND <> ''`). Es la combinación tipo + número y no el número solo, porque cédula, DIMEX y pasaportes de distintos países tienen numeraciones independientes que podrían coincidir por casualidad. El filtro deja fuera a las personas indocumentadas: sin él chocarían todas entre sí.
- `IX_Beneficiarios_Categoria` e `IX_Beneficiarios_Estado`, para los filtros del listado.

### `AsistenciasComedor`

| Columna | Tipo | |
|---|---|---|
| `Id` | `int` | PK |
| `BeneficiarioId` | `int` | FK → `Beneficiarios`, Restrict |
| `Fecha` | `date` | sin hora: la asistencia es del día |
| `TiempoComida` | `varchar(20)` | |

- `CK_AsistenciasComedor_TiempoComida`: solo `'Desayuno'`, `'Almuerzo'` o `'Merienda'`.
- `UX_AsistenciasComedor_Beneficiario_Fecha_TiempoComida` (único): un beneficiario no puede registrarse dos veces en el mismo tiempo de comida el mismo día.
- `IX` sueltos en `Fecha` y `TiempoComida`.

### `Articulos`

| Columna | Tipo | |
|---|---|---|
| `Id` | `int` | PK |
| `Nombre` | `varchar(150)` | **único** |
| `Codigo` | `varchar(50)` | opcional, **único cuando se define** (SKU corto, ej. "P001") |
| `Categoria` | `varchar(100)` | indexada |
| `UnidadMedida` | `varchar(50)` | etiquetas cortas: "Kilogramo", "Litro" |
| `Ubicacion` | `varchar(100)` | opcional, texto libre (bodega, estante) |
| `StockActual` | `int` | |
| `StockMinimo` | `int` | editable por artículo desde "Editar artículo"; por debajo de esto el listado marca stock bajo |

**`Nombre` es único (`UX_Articulos_Nombre`)** porque es la clave natural del catálogo: al registrar una entrada, el servicio busca el artículo por nombre y lo crea si no existe. Sin el índice, dos entradas simultáneas del mismo artículo nuevo crearían dos filas y el stock quedaría partido entre las dos.

**`Codigo` es único cuando se define (`UX_Articulos_Codigo`, filtrado)**: igual tratamiento que `NumIdentidad` en `Beneficiarios` — es opcional, así que el índice deja fuera a los artículos sin código para que no choquen entre sí.

`CK_Articulos_StockActual_NoNegativo` y `CK_Articulos_StockMinimo_NoNegativo`: el stock es un conteo físico y no puede ser negativo.

**Eliminar un artículo** solo se permite cuando no tiene historial: `InventarioService.EliminarArticuloAsync` consulta `TieneMovimientosAsync` (¿tiene entradas, salidas o solicitudes de préstamo?) antes de borrar, y explica por qué si lo tiene. El `Restrict` de las FK de `EntradasInventario`, `SalidasInventario` y `SolicitudesPrestamo` respalda esto en la base como última red.

### `EntradasInventario`

| Columna | Tipo | |
|---|---|---|
| `Id` | `int` | PK |
| `ArticuloId` | `int` | FK → `Articulos`, Restrict, obligatoria |
| `Cantidad` | `int` | > 0 |
| `Fecha` | `datetime2` | |
| `Origen` | `varchar(20)` | `'Donacion'` o `'Compra'` |
| `DonanteId` | `int` | opcional, **sin FK todavía** |
| `GastoOperativoId` | `int` | opcional, **sin FK todavía** |
| `Observaciones` | `varchar(500)` | opcional |

- `CK_EntradasInventario_Origen` y `CK_EntradasInventario_Cantidad`.
- `IX_EntradasInventario_Articulo_Fecha` (compuesto) para el historial, que filtra por artículo y rango de fechas a la vez. Al empezar por `ArticuloId`, EF lo reconoce como índice de la FK y no crea otro redundante.
- `IX_EntradasInventario_Fecha` suelto, porque el historial también se consulta por fecha sin filtrar por artículo (y ahí el compuesto no sirve: no se puede hacer seek por la segunda columna de un índice).

### `SalidasInventario`

| Columna | Tipo | |
|---|---|---|
| `Id` | `int` | PK |
| `ArticuloId` | `int` | FK → `Articulos`, Restrict, obligatoria |
| `Cantidad` | `int` | > 0 |
| `Fecha` | `datetime2` | |
| `TipoSalida` | `varchar(20)` | `'Donacion'` o `'Prestamo'` |
| `ComunidadDestinataria` | `varchar(150)` | opcional, solo en donaciones |
| `SolicitudPrestamoId` | `int` | opcional, FK → `SolicitudesPrestamo`, Restrict |
| `Observaciones` | `varchar(500)` | opcional |

`SolicitudPrestamoId` es una **FK real** y no un `int` suelto: el valor lo escribe la aprobación del préstamo a partir de una solicitud que acaba de leer, así que siempre apunta a una fila existente y la base puede garantizarlo. Es nullable porque las salidas por donación no nacen de una solicitud.

`UX_SalidasInventario_SolicitudPrestamo` (único, **filtrado** por `IS NOT NULL`): una solicitud aprobada genera **una sola** salida. Cierra la condición de carrera de dos aprobaciones simultáneas de la misma solicitud, que de otro modo pasarían las dos el chequeo de "ya fue resuelta" y descontarían el stock dos veces. El filtro permite que muchas salidas por donación convivan con la columna en `NULL`.

### `SolicitudesPrestamo`

| Columna | Tipo | |
|---|---|---|
| `Id` | `int` | PK |
| `ArticuloId` | `int` | FK → `Articulos`, Restrict, obligatoria |
| `Cantidad` | `int` | > 0 |
| `Fecha` | `datetime2` | |
| `Actividad` | `varchar(150)` | |
| `Solicitante` | `varchar(150)` | |
| `Estado` | `varchar(20)` | `'Pendiente'`, `'Aprobada'` o `'Rechazada'` |
| `MotivoRechazo` | `varchar(500)` | opcional |

**El enum `EstadoSolicitudPrestamo` se guarda como texto**, no como el entero del enum, usando el conversor integrado de EF Core:

```csharp
entity.Property(s => s.Estado)
    .IsRequired()
    .HasConversion<string>()
    .IsUnicode(false)
    .HasMaxLength(20);
```

Así la tabla se entiende leyéndola, el `CHECK` se puede escribir sobre valores con significado, y agregar o reordenar valores del enum más adelante no reinterpreta las filas ya guardadas.

`IX_SolicitudesPrestamo_Estado` sostiene la bandeja de solicitudes pendientes.

### Por qué las fechas de inventario son `datetime2` y las de asistencia son `date`

La asistencia es del día: no importa a qué hora comió alguien, y `date` hace el índice más chico. Los movimientos de inventario sí llevan hora, porque varios pueden caer el mismo día y la aprobación de un préstamo sella la salida con `DateTime.Now`. Sin la hora, el historial de una misma jornada quedaría en orden arbitrario.

Esto tiene una consecuencia al filtrar por rango: el límite superior se compara como **"menor que el día siguiente"**, nunca como "menor o igual que la fecha hasta". Comparar contra la medianoche del último día dejaría fuera todos los movimientos de esa jornada.

## Migraciones

En orden cronológico:

| Migración | Qué hace |
|---|---|
| `20260816002922_AddTablaBeneficiarios` | Tabla inicial de beneficiarios. |
| `20260821000745_AddTablaAsistenciaComedor` | Tabla de asistencias y su FK. |
| `20260821003516_AddCheckTiempoComida` | CHECK del dominio cerrado de tiempos de comida. |
| `20260821071358_CorrigeTiempoComidaMerienda` | Recrea el CHECK: "Cena" fue un error inicial; los tiempos son Desayuno, Almuerzo y Merienda. |
| `20260821090000_SeparaNombreYDerivaCategoriaBeneficiario` | Separa los apellidos en columnas propias y agrega el índice único de identidad. |
| `20260821120000_AmpliaNombresYValidacionesBeneficiario` | `Nombre` → `PrimerNombre`, agrega `SegundoNombre`, teléfono de 8 dígitos y normaliza los tipos de documento existentes. |
| `20260822120000_UnicidadDocumentoBeneficiario` | Normaliza los números guardados y crea el índice único filtrado de documento. |
| `20260828001822_AddTablaArticulosYEntradasInventario` | Tablas `Articulos` y `EntradasInventario`. |
| `20260828001907_AddTablaSalidasInventarioYSolicitudesPrestamo` | Tablas `SalidasInventario` y `SolicitudesPrestamo`. |
| `20260901010731_AddCodigoYUbicacionArticulo` | Agrega `Codigo` (único, filtrado) y `Ubicacion` a `Articulos`. |

Varias de estas migraciones llevan bloques `migrationBuilder.Sql(...)` que **arreglan los datos ya guardados** antes de apretar una restricción. Es deliberado: no alcanza con cambiar el esquema si las filas existentes no cumplen la regla nueva.

### Comandos

Se ejecutan desde la carpeta `SIGAC/` (la que contiene `SIGAC.slnx`):

```bash
# Crear una migración
dotnet ef migrations add NombreDeLaMigracion --project SIGAC.Infrastructure --startup-project SIGAC

# Ver cuáles están aplicadas y cuáles pendientes
dotnet ef migrations list --project SIGAC.Infrastructure --startup-project SIGAC

# Aplicar las pendientes
dotnet ef database update --project SIGAC.Infrastructure --startup-project SIGAC

# Deshacer la última migración TODAVÍA NO aplicada
dotnet ef migrations remove --project SIGAC.Infrastructure --startup-project SIGAC

# Generar el script SQL en vez de aplicarlo (recomendado para producción)
dotnet ef migrations script --idempotent --project SIGAC.Infrastructure --startup-project SIGAC --output migracion.sql
```

> `database update` aplica **todas** las migraciones pendientes en orden, no solo la última. Si tu base local está varias migraciones atrás, revisá antes que tus datos cumplan las restricciones nuevas: un índice único no se puede crear sobre una tabla que ya tiene duplicados, y la migración falla entera. En producción, usá el script idempotente y revisalo antes de correrlo.

## Repositorios

Los tres siguen el mismo patrón: reciben `IDbContextFactory<SigacDbContext>`, cada método abre su propio contexto con `await using`, las lecturas van con `AsNoTracking()` y se materializan con `ToListAsync()` antes de devolver (el contexto se libera al salir del método, así que un `IQueryable` diferido explotaría al recorrerlo desde la página).

Los filtros se componen sobre el `IQueryable` para que viajen a la base como `WHERE`. Nada de LINQ to Objects: filtrar en memoria obligaría a traerse la tabla entera.

Decisiones que conviene conocer antes de tocar `InventarioRepositoryEfCore`:

- **`ActualizarStockAsync` y `ReducirStockAsync` usan `ExecuteUpdate`**, que se traduce a un único `UPDATE Articulos SET StockActual = StockActual + @n`. Leer, modificar en memoria y guardar dejaría que dos movimientos simultáneos del mismo artículo lean el mismo stock inicial y uno pise al otro. `ReducirStockAsync` además lleva la condición "hay stock suficiente" dentro del `WHERE`, y si no afecta ninguna fila lanza una excepción en vez de callarse: la salida ya quedó insertada por la llamada anterior y un fallo silencioso dejaría el movimiento registrado sin su descuento.
- **`ActualizarArticuloAsync` copia campo por campo** en lugar de usar `Update()`. `Update()` marca *todas* las columnas como modificadas, incluida `StockActual`, y reescribiría el valor viejo que traía la entidad desprendida, pisando cualquier entrada registrada entre la lectura y el guardado. El stock solo cambia por los métodos de stock.
- **`ObtenerSolicitudPorIdAsync` no hace `Include` del artículo a propósito.** La solicitud vuelve a `ActualizarSolicitudAsync` para guardarse, y un artículo colgado de la navegación podría arrastrarse a ese guardado y pisarle el stock. `ObtenerSolicitudesAsync` sí lo incluye, porque es solo lectura.
- **La búsqueda por nombre de artículo usa igualdad directa, sin collation**, para que sea exactamente la misma comparación que hace `UX_Articulos_Nombre` y pueda hacer seek sobre ese índice. La collation acentuada-insensible (`Latin1_General_CI_AI`) se reserva para las cajas de búsqueda del listado, donde quien escribe "azucar" espera encontrar "Azúcar".
- **`ObtenerExistenciasAsync` pagina en el servidor** (mismo patrón que `BeneficiariosRepositoryEfCore.ObtenerPaginaAsync`): dos consultas, un `CountAsync` para el total y un `Skip`/`Take` para la página. La caja de búsqueda filtra por `Nombre` o por `Codigo` a la vez, para que buscar "P001" encuentre el artículo sin cambiar de campo.
- **`EliminarArticuloAsync` no valida nada por su cuenta.** El chequeo de "¿tiene movimientos?" vive en `TieneMovimientosAsync`, separado, porque es el servicio (`InventarioService.EliminarArticuloAsync`) quien decide qué hacer con la respuesta y arma el mensaje. El repositorio solo ejecuta lo que se le pide.

## Detalles que confunden la primera vez

- **`Restrict` aparece como `NO_ACTION` en SQL Server.** Si inspeccionás `sys.foreign_keys` vas a ver `NO_ACTION` en el `delete_referential_action_desc`. Es correcto: EF Core traduce así su `DeleteBehavior.Restrict`, y el borrado igual se rechaza.
- **`sqlcmd` no puede escribir en tablas con índice filtrado** salvo que le pases `-I`. Conecta con `QUOTED_IDENTIFIER OFF` por defecto y SQL Server exige `ON` para modificar esas tablas. Afecta a `Beneficiarios` y a `SalidasInventario`. La aplicación no tiene el problema: `SqlClient` siempre lo pone en `ON`.
- **Dos `NULL` no son iguales para un índice único de SQL Server.** Por eso hay columnas que se guardan como cadena vacía y por eso los índices que deben ignorar los nulos van filtrados. Es la razón detrás de varias decisiones que si no parecerían arbitrarias.

## Pendiente

- **`EntradaInventario.DonanteId` y `GastoOperativoId` no tienen FK.** Las entidades `Donante` y `GastoOperativo` todavía no existen en el dominio, así que hoy son columnas `int NULL` sin integridad referencial: se puede guardar un id que no corresponda a nada. La configuración está escrita y comentada con un `TODO` en `SigacDbContext`, lista para descomentar y generar una migración cuando esas entidades se creen.
