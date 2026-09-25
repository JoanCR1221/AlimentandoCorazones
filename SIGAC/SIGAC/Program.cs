using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Infrastructure.Data;
using SIGAC.Infrastructure.Identity;
using SIGAC.Infrastructure.Repositories;
using SIGAC.Components;
using SIGAC.Services;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<AparienciaService>();

builder.Services.AddMudServices();

// DbContext de EF Core contra SQL Server Express local.
// Factory en lugar de AddDbContext: en Blazor Server el scope dura toda la
// sesión (circuito), no cada clic, así que un DbContext inyectado directo
// queda compartido entre operaciones concurrentes y EF Core no tolera eso
// (DbContext no es seguro para usarse desde dos operaciones a la vez). Cada
// repositorio pide un contexto nuevo y de corta vida por operación.
builder.Services.AddDbContextFactory<SigacDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SigacDb")));

// ---- Autenticación y usuarios (ASP.NET Identity) ----
//
// AddIdentityCore + AddIdentityCookies en vez de AddIdentity: es la combinación
// que usa la plantilla oficial de Blazor Web App. AddIdentity arrastra los
// redireccionamientos de las Razor Pages de Identity, que acá no existen.
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddIdentityCore<UsuarioSigac>(options =>
    {
        // El correo es el nombre de usuario: no puede repetirse.
        options.User.RequireUniqueEmail = true;

        // Reglas de contraseña (AB#1215): 8 caracteres con mayúscula, minúscula,
        // número y símbolo. Es lo que pide la política de seguridad del documento
        // de visión (OWASP), y el mensaje de la pantalla las repite tal cual.
        options.Password.RequiredLength = ReglasUsuario.LongitudMinimaPassword;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;

        // Bloqueo temporal tras intentos fallidos, para frenar adivinación de
        // contraseñas desde la LAN. La desactivación de un usuario usa el mismo
        // mecanismo con LockoutEnd = MaxValue (ver UsuariosService).
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = ReglasUsuario.MaximoIntentosFallidos;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(ReglasUsuario.MinutosBloqueo);

        // Sin confirmación por correo: el sistema no tiene internet ni servidor de
        // correo, y las cuentas las crea un administrador en persona.
        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<SigacDbContext>()
    .AddSignInManager()
    // Claims propios en la cookie: nombre para mostrar y un claim por permiso
    // efectivo (rol menos revocados).
    .AddClaimsPrincipalFactory<PermisosClaimsPrincipalFactory>()
    // Mensajes de error de Identity en español y en lenguaje simple.
    .AddErrorDescriber<ErroresIdentityEs>()
    .AddDefaultTokenProviders();

// Gestión de usuarios (registrar, listar, rol, estado, contraseñas). Vive en
// Infrastructure porque usa UserManager; Application solo conoce la interfaz.
builder.Services.AddScoped<IUsuariosService, UsuariosService>();

// La cookie se revalida contra el security stamp cada minuto también en las
// cargas de página completas (el provider de Blazor cubre solo el circuito).
// Default: 30 minutos, demasiado para "el cambio se refleja de inmediato".
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

// Dentro del circuito interactivo, lo mismo cada minuto.
builder.Services.AddScoped<AuthenticationStateProvider, RevalidacionSesionProvider>();

// Quién tiene la sesión, para la bitácora y para "nadie se modifica a sí mismo".
builder.Services.AddScoped<IUsuarioActual, UsuarioActual>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/acceso-denegado";
    options.LogoutPath = "/cuenta/logout";

    // Una jornada de trabajo. Deslizante: mientras se use, no vence.
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    // Una policy por permiso del catálogo, con el mismo nombre que la clave:
    // [Authorize(Policy = Permisos.Gastos.Anular)] y
    // <AuthorizeView Policy="@Permisos.Gastos.Anular"> exigen el claim
    // correspondiente en la cookie. Agregar un permiso a Permisos.Definiciones
    // lo registra acá solo, sin tocar este archivo.
    foreach (var permiso in Permisos.Todos)
        options.AddPolicy(permiso, policy => policy.RequireClaim(ClaimsSigac.Permiso, permiso));

    // Todo lo que no diga lo contrario exige sesión: cualquier página o endpoint
    // nuevo nace protegido. Lo público (Login, Acceso denegado, No encontrado,
    // Error) se marca con [AllowAnonymous] explícitamente.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Servicios del módulo de Beneficiarios y Asistencia
builder.Services.AddScoped<IBeneficiariosService, BeneficiariosService>();
builder.Services.AddScoped<IAsistenciaService, AsistenciaService>();

// Servicio del módulo de Control de Inventario
builder.Services.AddScoped<IInventarioService, InventarioService>();

// Servicio del módulo de Gastos Operativos
builder.Services.AddScoped<IGastosService, GastosService>();

// Servicios del módulo de Gestión de Donaciones
builder.Services.AddScoped<IDonantesService, DonantesService>();
builder.Services.AddScoped<IDonacionesService, DonacionesService>();

// Servicio del módulo de Gestión de Proyectos
builder.Services.AddScoped<IProyectosService, ProyectosService>();

// Cifras de las tarjetas de resumen de los módulos (solo lectura)
builder.Services.AddScoped<IResumenService, ResumenService>();


// Repositorio de Beneficiarios con EF Core (reemplaza la versión temporal en memoria)
builder.Services.AddScoped<IBeneficiariosRepository, BeneficiariosRepositoryEfCore>();

// Repositorio de Asistencia TEMPORAL en memoria (aún sin migrar a EF Core)
builder.Services.AddScoped<IAsistenciaRepository, AsistenciaRepositoryEfCore>();

// Repositorio de Inventario con EF Core (reemplaza la versión temporal en memoria)
builder.Services.AddScoped<IInventarioRepository, InventarioRepositoryEfCore>();

// Repositorio de Gastos Operativos con EF Core (reemplaza la versión temporal en memoria)
builder.Services.AddScoped<IGastosRepository, GastosRepositoryEfCore>();

// Repositorios de Donantes y Donaciones con EF Core
builder.Services.AddScoped<IDonantesRepository, DonantesRepositoryEfCore>();
builder.Services.AddScoped<IDonacionesRepository, DonacionesRepositoryEfCore>();

// Repositorio de Proyectos Comunitarios con EF Core
builder.Services.AddScoped<IProyectosRepository, ProyectosRepositoryEfCore>();

// Consultas agregadas de las tarjetas de resumen
builder.Services.AddScoped<IResumenRepository, ResumenRepositoryEfCore>();

// Repositorio de permisos revocados por usuario (módulo de seguridad). Lo usa
// PermisosClaimsPrincipalFactory al iniciar sesión y el panel de permisos.
builder.Services.AddScoped<IPermisosRepository, PermisosRepositoryEfCore>();

// Bitácora de acciones (AB#2773): la escriben todos los servicios y las piezas
// de seguridad; la lee la pantalla /bitacora.
builder.Services.AddScoped<IBitacoraRepository, BitacoraRepositoryEfCore>();
builder.Services.AddScoped<IBitacoraService, BitacoraService>();


var app = builder.Build();

// Administrador inicial (AB#921): solo actúa cuando no existe ningún
// Administrador activo, con las credenciales de la sección AdministradorInicial
// de appsettings.json. Falla al arrancar, con mensaje claro, si faltan.
await SeedSeguridad.CrearAdministradorInicialAsync(app.Services, app.Configuration);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Antes de UseAntiforgery y de MapRazorComponents: la cookie de sesión tiene
// que estar leída para que [Authorize] y AuthorizeView sepan quién es el usuario.
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Los assets estáticos (CSS, JS, logo, fuentes de MudBlazor) tienen que servirse
// sin sesión: la página de Login los necesita. Sin esto, el FallbackPolicy los
// redirigiría al login y la pantalla saldría sin estilos.
app.MapStaticAssets()
    .AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Cierre de sesión (AB#2752). Endpoint mínimo y no un método de servicio:
// SignOutAsync tiene que borrar la cookie en la respuesta HTTP, y dentro del
// circuito interactivo de Blazor la respuesta ya se envió. Es POST con token
// antiforgery (lo valida el middleware por el parámetro [FromForm]) para que un
// enlace externo no pueda cerrarle la sesión a alguien.
app.MapPost("/cuenta/logout", async (
    HttpContext http,
    SignInManager<UsuarioSigac> signInManager,
    IBitacoraService bitacora,
    [FromForm] string? origen) =>
{
    // Los datos salen de los claims de la petición (acá no hay
    // AuthenticationStateProvider) y se registran ANTES de borrar la cookie.
    var usuario = http.User;
    if (usuario.Identity?.IsAuthenticated == true)
    {
        await bitacora.RegistrarDeUsuarioAsync(
            usuario.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            usuario.FindFirst(ClaimsSigac.Nombre)?.Value ?? usuario.Identity.Name ?? string.Empty,
            usuario.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
            AccionesBitacora.CerrarSesion,
            ModulosSistema.Seguridad,
            usuario.Identity.Name);
    }

    await signInManager.SignOutAsync();
    return TypedResults.LocalRedirect("/login");
});

app.Run();