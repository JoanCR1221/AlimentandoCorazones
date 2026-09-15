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
}
