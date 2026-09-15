using System.ComponentModel.DataAnnotations;

namespace SIGAC.Application.DTOs.Seguridad
{
    // Credenciales del formulario de inicio de sesión. Solo DataAnnotations
    // básicas: la validación real (usuario existe, activo, contraseña correcta,
    // bloqueo por intentos) la hace Identity en la página de Login.
    public class LoginDto
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Escribí un correo válido.")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Password { get; set; } = string.Empty;
    }

    // Alta de usuario (AB#2562). Sin campo Rol: todo usuario nuevo nace como
    // Asistente (RolesSistema.PorDefecto) y el rol se cambia desde el listado.
    public class UsuarioCrearDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Escribí un correo válido.")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repetí la contraseña.")]
        public string ConfirmarPassword { get; set; } = string.Empty;
    }

    public class UsuarioListaDto
    {
        public string Id { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;

        // Null solo si el usuario quedó sin rol por una edición externa; el
        // servicio siempre asigna uno.
        public string? Rol { get; set; }

        public bool Estado { get; set; }
        public DateTime FechaRegistro { get; set; }

        // Bloqueo temporal vigente por intentos fallidos (distinto de Estado =
        // false, que es la desactivación). Lo muestra el listado para que el
        // administrador entienda por qué alguien no puede entrar.
        public bool BloqueadoTemporalmente { get; set; }
    }

    public class FiltrosUsuarioDto
    {
        // Busca en nombre y correo a la vez.
        public string? Texto { get; set; }
        public string? Rol { get; set; }
        public bool? Estado { get; set; }
    }

    public class CambioRolDto
    {
        public string UsuarioId { get; set; } = string.Empty;
        public string NuevoRol { get; set; } = string.Empty;
    }

    // Cambio de la propia contraseña, por el usuario en sesión.
    public class CambioPasswordDto
    {
        [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
        public string PasswordActual { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña nueva es obligatoria.")]
        public string PasswordNueva { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repetí la contraseña nueva.")]
        public string ConfirmarPassword { get; set; } = string.Empty;
    }

    // Un switch del panel de permisos: la clave, el módulo que la agrupa, el texto
    // que ve el administrador y si está encendida (no revocada) para ese usuario.
    public class PermisoUsuarioDto
    {
        public string Permiso { get; set; } = string.Empty;
        public string Modulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public bool Habilitado { get; set; }
    }

    // Lo que manda el panel al guardar: solo las claves apagadas. Reemplaza el
    // conjunto completo de revocaciones del usuario.
    public class ActualizarPermisosDto
    {
        public string UsuarioId { get; set; } = string.Empty;
        public List<string> PermisosRevocados { get; set; } = new();
    }

    // Restablecimiento por un administrador: asigna una contraseña temporal a otro
    // usuario, que después la cambia desde Configuración.
    public class RestablecerPasswordDto
    {
        public string UsuarioId { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña temporal es obligatoria.")]
        public string PasswordTemporal { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repetí la contraseña temporal.")]
        public string ConfirmarPassword { get; set; } = string.Empty;
    }
}
