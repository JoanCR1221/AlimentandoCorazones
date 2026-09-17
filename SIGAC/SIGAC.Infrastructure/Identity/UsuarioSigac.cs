using Microsoft.AspNetCore.Identity;

namespace SIGAC.Infrastructure.Identity
{
    // Usuario del sistema. Extiende IdentityUser (clave string, tabla AspNetUsers)
    // en vez de ser una entidad propia del dominio: la contraseña, el bloqueo por
    // intentos fallidos y el security stamp los administra ASP.NET Identity y no
    // hay motivo para reimplementarlos.
    //
    // Vive en Infrastructure y no en SIGAC.Domain a propósito: Domain no conoce
    // Identity, igual que no conoce EF Core. Application solo ve DTOs
    // (UsuarioListaDto, UsuarioCrearDto...) y la interfaz IUsuariosService.
    //
    // Excepción documentada a las convenciones del mapeo (ver SigacDbContext): las
    // siete tablas de Identity conservan sus nombres en inglés, claves string y
    // columnas nvarchar tal como las genera el framework. Solo las dos columnas
    // propias siguen la convención varchar del proyecto.
    public class UsuarioSigac : IdentityUser
    {
        // Nombre para mostrar (AppBar, listado, bitácora). El correo es el
        // UserName de Identity y sirve para autenticarse; este es cómo se llama
        // la persona.
        public string Nombre { get; set; } = string.Empty;

        // Baja lógica, mismo patrón que Beneficiario y Donante: un usuario
        // desactivado no puede iniciar sesión pero conserva su historial en la
        // bitácora. Se acompaña de LockoutEnd = DateTimeOffset.MaxValue para que
        // Identity rechace el login por su cuenta, y este campo es el que consulta
        // el listado y el que se muestra en pantalla.
        public bool Estado { get; set; } = true;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
