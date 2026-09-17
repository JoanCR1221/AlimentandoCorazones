using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SIGAC.Domain;

namespace SIGAC.Infrastructure.Identity
{
    // Crea el usuario administrador inicial al arrancar la aplicación, solo si
    // todavía no existe ningún Administrador activo. Sin él nadie podría iniciar
    // sesión en una instalación nueva, porque la creación de usuarios está
    // restringida a administradores.
    //
    // Va en código y no en la migración (a diferencia de los roles, que sí se
    // siembran en AddTablaUsuariosIdentity) porque el hash de la contraseña lo
    // genera PasswordHasher en tiempo de ejecución y no se puede escribir a mano
    // en un INSERT.
    //
    // Las credenciales salen de configuración (sección "AdministradorInicial" de
    // appsettings.json, que está fuera del repositorio) y nunca del código.
    public static class SeedSeguridad
    {
        public const string SeccionConfiguracion = "AdministradorInicial";

        public static async Task CrearAdministradorInicialAsync(IServiceProvider servicios, IConfiguration configuracion)
        {
            using var scope = servicios.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioSigac>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Los roles los crea la migración; si faltan, la base no está al día y
            // conviene fallar temprano con un mensaje claro en vez de con una FK
            // violada más adelante.
            foreach (var rol in RolesSistema.Todos)
            {
                if (!await roleManager.RoleExistsAsync(rol))
                    throw new InvalidOperationException(
                        $"Falta el rol '{rol}' en la base de datos. Aplicá las migraciones pendientes (dotnet ef database update).");
            }

            // Basta con que exista uno activo. Uno desactivado no cuenta: si el único
            // administrador quedó inactivo por un error, esto permite recuperar el
            // acceso reiniciando la aplicación con la configuración correcta.
            var administradores = await userManager.GetUsersInRoleAsync(RolesSistema.Administrador);
            if (administradores.Any(a => a.Estado))
                return;

            var seccion = configuracion.GetSection(SeccionConfiguracion);
            var correo = seccion["Correo"];
            var nombre = seccion["Nombre"];
            var password = seccion["Password"];

            if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"No hay ningún administrador activo y falta la sección '{SeccionConfiguracion}' " +
                    "(Correo, Nombre, Password) en appsettings.json para crear el inicial. " +
                    "Ver appsettings.example.json.");
            }

            var administrador = new UsuarioSigac
            {
                UserName = correo,
                Email = correo,
                Nombre = nombre.Trim(),
                Estado = true,
                FechaRegistro = DateTime.Now
            };

            var creacion = await userManager.CreateAsync(administrador, password);
            if (!creacion.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo crear el administrador inicial: " + DescribirErrores(creacion));

            var asignacion = await userManager.AddToRoleAsync(administrador, RolesSistema.Administrador);
            if (!asignacion.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo asignar el rol Administrador al usuario inicial: " + DescribirErrores(asignacion));
        }

        private static string DescribirErrores(IdentityResult resultado) =>
            string.Join("; ", resultado.Errors.Select(e => e.Description));
    }
}
