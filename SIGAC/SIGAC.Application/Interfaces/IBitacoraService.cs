using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Bitacora;

namespace SIGAC.Application.Interfaces
{
    // Registro y consulta de la bitácora. Lo llaman todos los servicios del
    // sistema después de cada escritura (registrar, editar, anular...) y las
    // piezas de seguridad (login, logout, acceso denegado, gestión de usuarios).
    public interface IBitacoraService
    {
        // Con el usuario en sesión (IUsuarioActual). Si no hay sesión, la fila
        // queda sin usuario, con "sin sesión" como nombre.
        Task RegistrarAsync(string accion, string modulo, string? detalle = null);

        // Con los datos del usuario explícitos, para los dos casos en que la
        // sesión no está disponible por la vía normal: el inicio de sesión (la
        // cookie todavía no existe en esa petición) y el cierre (endpoint mínimo,
        // sin AuthenticationStateProvider). UsuarioId puede ser null (login
        // fallido con un correo que no existe).
        Task RegistrarDeUsuarioAsync(
            string? usuarioId, string nombreUsuario, string? rol,
            string accion, string modulo, string? detalle = null);

        Task<ResultadoPaginado<BitacoraAccionDto>> ObtenerBitacoraAsync(FiltrosBitacoraDto filtros);
    }
}
