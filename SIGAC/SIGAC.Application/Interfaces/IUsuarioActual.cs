namespace SIGAC.Application.Interfaces
{
    // Datos del usuario que tiene la sesión abierta. Es lo único que los
    // servicios de Application saben de la sesión: no conocen HttpContext,
    // AuthenticationStateProvider ni Identity. Lo implementa el proyecto web
    // (SIGAC.Services.UsuarioActual) y lo consumen la bitácora y las reglas de
    // "nadie se modifica a sí mismo" de UsuariosService.
    public sealed record DatosUsuarioActual(string Id, string Nombre, string Correo, string? Rol);

    public interface IUsuarioActual
    {
        // Null cuando no hay sesión (páginas públicas, login fallido).
        Task<DatosUsuarioActual?> ObtenerAsync();
    }
}
