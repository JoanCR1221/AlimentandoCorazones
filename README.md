# SIGAC

Aplicación web local construida con .NET 10 y Blazor Server sobre una arquitectura por capas.

## Requisitos previos

- .NET SDK 10.0 o superior
- SQL Server accesible localmente. Dos opciones:
  - **LocalDB** — viene incluido con Visual Studio, no requiere instalación aparte. Verificá si lo tenés con `sqllocaldb info` en una terminal.
  - **SQL Server Express** — instalación separada, disponible en el sitio de Microsoft.
- La herramienta `dotnet-ef`, necesaria para aplicar las migraciones desde una terminal:

```bash
dotnet tool install --global dotnet-ef
```

- El proyecto host `SIGAC` debe referenciar `Microsoft.EntityFrameworkCore.Design`. El paquete está declarado en `SIGAC.Infrastructure` con `PrivateAssets="all"`, por lo que no se propaga al host y hay que agregarlo también ahí:

```bash
dotnet add SIGAC/SIGAC.csproj package Microsoft.EntityFrameworkCore.Design --version 10.0.10
```

## Estructura de la solución

| Proyecto | Responsabilidad |
| --- | --- |
| `SIGAC.Domain` | Entidades del dominio. Sin dependencias externas. |
| `SIGAC.Application` | Casos de uso: DTOs, interfaces, servicios y excepciones de negocio. |
| `SIGAC.Infrastructure` | Persistencia: `SigacDbContext`, repositorios EF Core y migraciones. |
| `SIGAC` | Host web Blazor Server. Composition root e interfaz de usuario. |

## Configuración local

El archivo `SIGAC/SIGAC/appsettings.json` **no está versionado**: cada desarrollador usa su propia cadena de conexión. Al clonar el repositorio hay que crearlo siguiendo estos tres pasos.

### 1. Copiar la plantilla

Desde la raíz del repositorio:

```bash
cp SIGAC/SIGAC/appsettings.example.json SIGAC/SIGAC/appsettings.json
```

En PowerShell:

```powershell
Copy-Item SIGAC\SIGAC\appsettings.example.json SIGAC\SIGAC\appsettings.json
```

El archivo nuevo queda ignorado por git, así que tu cadena de conexión nunca se commitea.

### 2. Ajustar la cadena de conexión

Abrí `SIGAC/SIGAC/appsettings.json` y editá `ConnectionStrings:SigacDb` según el motor que uses.

Con **SQL Server Express**:

```json
{
  "ConnectionStrings": {
    "SigacDb": "Server=localhost\\SQLEXPRESS;Database=SIGAC;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Con **LocalDB**:

```json
{
  "ConnectionStrings": {
    "SigacDb": "Server=(localdb)\\MSSQLLocalDB;Database=SIGAC;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Detalles a tener en cuenta:

- **En JSON la barra invertida se escribe duplicada (`\\`)**, aunque el valor real tenga una sola. Con una sola barra el archivo queda inválido.
- Si tenés una instancia por defecto de SQL Server, `Server=localhost` a secas alcanza.
- `Database`: nombre de la base que se va a crear. Podés dejar `SIGAC`.
- `Trusted_Connection=True` usa autenticación integrada de Windows, así que no hace falta guardar usuario ni contraseña en el archivo.
- `TrustServerCertificate=True` es necesario en desarrollo porque SQL Server usa un certificado autofirmado.
- La clave debe llamarse exactamente `SigacDb`: es la que lee `Program.cs` al registrar el `DbContext`. Si falta o está mal escrita, la aplicación arranca pero falla al primer acceso a datos.

### 3. Aplicar las migraciones

Las migraciones viven en `SIGAC.Infrastructure`, pero la cadena de conexión y el registro del `DbContext` están en el proyecto host `SIGAC`. Como la solución no define un `IDesignTimeDbContextFactory`, hay que indicarle los dos proyectos a la herramienta.

Desde una terminal, parado en la carpeta `SIGAC/` (la que contiene `SIGAC.slnx`):

```bash
dotnet ef database update --project SIGAC.Infrastructure --startup-project SIGAC
```

Desde Visual Studio, el equivalente va en la **Consola del Administrador de paquetes** (Herramientas → Administrador de paquetes NuGet → Consola del Administrador de paquetes):

```powershell
Update-Database -Project SIGAC.Infrastructure -StartupProject SIGAC
```

Estos cmdlets solo existen en esa consola: en una terminal normal hay que usar `dotnet ef`.

Eso crea la base de datos y aplica las migraciones existentes:

| Migración | Qué hace |
| --- | --- |
| `AddTablaBeneficiarios` | Crea la tabla `Beneficiarios` con sus índices de búsqueda. |
| `AddTablaAsistenciaComedor` | Crea `AsistenciasComedor`, su clave foránea y el índice único por beneficiario, fecha y tiempo de comida. |
| `AddCheckTiempoComida` | Agrega el CHECK que restringe `TiempoComida` a `Desayuno`, `Almuerzo` o `Cena`. |

## Ejecutar la aplicación

Desde la carpeta `SIGAC/`:

```bash
dotnet run --project SIGAC
```

La aplicación queda disponible en `https://localhost:7093` o `http://localhost:5175`, según los perfiles definidos en `SIGAC/SIGAC/Properties/launchSettings.json`.