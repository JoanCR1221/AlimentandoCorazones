using SIGAC.Application.DTOs.Seguridad;

namespace SIGAC.Application.Interfaces
{
    // Gestión de usuarios del sistema (PBI 1944, 1945, 1946 y contraseñas). La
    // implementación vive en Infrastructure porque usa UserManager de Identity;
    // Application solo ve DTOs. Iniciar y cerrar sesión NO están acá: escriben la
    // cookie y viven en la página de Login y en el endpoint /cuenta/logout.
    //
    // Reglas que aplica la implementación en cada operación:
    // - Nadie cambia su propio rol, ni se desactiva, ni se restablece su propia
    //   contraseña por esta vía (para eso está CambiarPasswordAsync).
    // - Siempre queda al menos un Administrador activo (UltimoAdministradorException).
    // - Cambiar rol, permisos o estado actualiza el security stamp: la sesión
    //   activa del usuario afectado se cierra en menos de un minuto.
    public interface IUsuariosService
    {
        // El usuario nuevo nace activo y con el rol Asistente.
        Task RegistrarUsuarioAsync(UsuarioCrearDto dto);

        Task<IReadOnlyList<UsuarioListaDto>> ObtenerUsuariosAsync(FiltrosUsuarioDto filtros);
        Task<UsuarioListaDto?> ObtenerPorIdAsync(string usuarioId);

        // Al cambiar de rol se borran las revocaciones de permisos del usuario:
        // eran recortes sobre el rol anterior y no se heredan.
        Task CambiarRolAsync(CambioRolDto dto);

        Task ActivarUsuarioAsync(string usuarioId);
        Task DesactivarUsuarioAsync(string usuarioId);

        // Del usuario en sesión, con la contraseña actual como comprobación.
        Task CambiarPasswordAsync(CambioPasswordDto dto);

        // De otro usuario, por un administrador: asigna una contraseña temporal y
        // levanta el bloqueo por intentos fallidos si lo hubiera.
        Task RestablecerPasswordAsync(RestablecerPasswordDto dto);
    }
}
