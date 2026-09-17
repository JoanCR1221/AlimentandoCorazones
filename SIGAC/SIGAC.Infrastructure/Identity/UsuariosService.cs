using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIGAC.Application.DTOs.Seguridad;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Infrastructure.Data;

namespace SIGAC.Infrastructure.Identity
{
    // Implementación de IUsuariosService sobre ASP.NET Identity. Vive en
    // Infrastructure y no en Application porque depende de UserManager, y sigue
    // el mismo esqueleto que los servicios de Application: valida con
    // UsuarioValidator, deja pasar ValidationException/NotFoundException/
    // DuplicateException y envuelve cualquier otro error con un mensaje simple.
    public class UsuariosService : IUsuariosService
    {
        private const string ColacionSinTildes = "Latin1_General_CI_AI";

        private readonly UserManager<UsuarioSigac> _userManager;
        private readonly IUsuarioActual _usuarioActual;
        private readonly IPermisosRepository _permisos;

        // El MISMO SigacDbContext scoped que usa el UserStore por dentro (los dos
        // salen del contenedor en el mismo scope). Eso es lo que permite abrir una
        // transacción acá y que los SaveChanges de UserManager caigan adentro:
        // "contar administradores activos" y "quitar el rol" son una sola
        // operación atómica. Un contexto de la factory sería otra conexión y la
        // transacción no cubriría a UserManager.
        private readonly SigacDbContext _context;

        private readonly IBitacoraService _bitacora;

        public UsuariosService(
            UserManager<UsuarioSigac> userManager,
            IUsuarioActual usuarioActual,
            IPermisosRepository permisos,
            IBitacoraService bitacora,
            SigacDbContext context)
        {
            _userManager = userManager;
            _usuarioActual = usuarioActual;
            _permisos = permisos;
            _bitacora = bitacora;
            _context = context;
        }

        // ------------------------------------------------------------------
        // Alta
        // ------------------------------------------------------------------

        public async Task RegistrarUsuarioAsync(UsuarioCrearDto dto)
        {
            try
            {
                var datos = UsuarioValidator.Validar(dto);

                // Chequeo previo para dar un DuplicateException con mensaje claro;
                // el índice único de Identity (NormalizedEmail / NormalizedUserName)
                // cierra la condición de carrera si dos altas entran a la vez.
                if (await _userManager.FindByEmailAsync(datos.Correo) is not null)
                    throw new DuplicateException("Ya existe un usuario con ese correo.");

                var usuario = new UsuarioSigac
                {
                    UserName = datos.Correo,
                    Email = datos.Correo,
                    Nombre = datos.Nombre,
                    Estado = true,
                    FechaRegistro = DateTime.Now
                };

                // Crear el usuario y asignarle el rol es todo o nada: un usuario
                // sin rol no tendría ningún permiso y confundiría al listado.
                await using var transaccion = await _context.Database.BeginTransactionAsync();

                Exigir(await _userManager.CreateAsync(usuario, datos.Password));
                Exigir(await _userManager.AddToRoleAsync(usuario, RolesSistema.PorDefecto));

                await transaccion.CommitAsync();

                // Después del commit: la bitácora escribe por su propia conexión y no
                // debe quedar atada a esta transacción.
                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Seguridad,
                    $"Usuario {usuario.Nombre} ({usuario.Email}), rol {RolesSistema.PorDefecto}");
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al registrar el usuario.", ex);
            }
        }

        // ------------------------------------------------------------------
        // Consultas
        // ------------------------------------------------------------------

        public async Task<IReadOnlyList<UsuarioListaDto>> ObtenerUsuariosAsync(FiltrosUsuarioDto filtros)
        {
            try
            {
                var consulta = ConsultaConRol();

                if (!string.IsNullOrWhiteSpace(filtros.Texto))
                {
                    var texto = filtros.Texto.Trim();
                    consulta = consulta.Where(x =>
                        EF.Functions.Collate(x.Usuario.Nombre, ColacionSinTildes).Contains(texto)
                        || (x.Usuario.Email != null && x.Usuario.Email.Contains(texto)));
                }

                if (!string.IsNullOrWhiteSpace(filtros.Rol))
                {
                    var rol = filtros.Rol;
                    consulta = consulta.Where(x => x.Rol == rol);
                }

                if (filtros.Estado.HasValue)
                {
                    var estado = filtros.Estado.Value;
                    consulta = consulta.Where(x => x.Usuario.Estado == estado);
                }

                // Sin paginar: el volumen de usuarios de la asociación es de decenas,
                // no de miles (mismo criterio que el listado de gastos).
                var filas = await consulta
                    .OrderBy(x => x.Usuario.Nombre)
                    .ThenBy(x => x.Usuario.Email)
                    .ToListAsync();

                var ahora = DateTimeOffset.Now;

                return filas.Select(x => Proyectar(x.Usuario, x.Rol, ahora)).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar los usuarios.", ex);
            }
        }

        public async Task<UsuarioListaDto?> ObtenerPorIdAsync(string usuarioId)
        {
            try
            {
                var fila = await ConsultaConRol()
                    .FirstOrDefaultAsync(x => x.Usuario.Id == usuarioId);

                return fila is null ? null : Proyectar(fila.Usuario, fila.Rol, DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el usuario.", ex);
            }
        }

        // Usuario + nombre de su rol en una sola consulta (left join: un usuario sin
        // rol por edición externa igual aparece, con Rol en null).
        //
        // Proyección con inicializador de miembros y no con constructor: EF Core
        // solo "ve adentro" de proyecciones anónimas o de inicializadores, así que
        // los Where/OrderBy que vienen después (x.Usuario.Nombre, x.Rol) se
        // traducen a SQL. Con un record por constructor no podría y fallaría.
        private IQueryable<UsuarioConRol> ConsultaConRol() =>
            from u in _context.Users.AsNoTracking()
            join ur in _context.UserRoles on u.Id equals ur.UserId into rolesDelUsuario
            from ur in rolesDelUsuario.DefaultIfEmpty()
            join r in _context.Roles on ur.RoleId equals r.Id into roles
            from r in roles.DefaultIfEmpty()
            select new UsuarioConRol { Usuario = u, Rol = r != null ? r.Name : null };

        private sealed class UsuarioConRol
        {
            public required UsuarioSigac Usuario { get; init; }
            public string? Rol { get; init; }
        }

        private static UsuarioListaDto Proyectar(UsuarioSigac usuario, string? rol, DateTimeOffset ahora) => new()
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = usuario.Email ?? string.Empty,
            Rol = rol,
            Estado = usuario.Estado,
            FechaRegistro = usuario.FechaRegistro,
            // Un usuario desactivado también tiene LockoutEnd (en MaxValue), pero
            // eso ya lo dice Estado: acá solo se marca el bloqueo temporal.
            BloqueadoTemporalmente = usuario.Estado
                && usuario.LockoutEnd.HasValue
                && usuario.LockoutEnd.Value > ahora
        };

        // ------------------------------------------------------------------
        // Rol
        // ------------------------------------------------------------------

        public async Task CambiarRolAsync(CambioRolDto dto)
        {
            try
            {
                if (!RolesSistema.EsValido(dto.NuevoRol))
                    throw new ValidationException("El rol indicado no existe.");

                await ExigirQueNoSeaElMismoAsync(dto.UsuarioId,
                    "No podés cambiar tu propio rol. Pedile a otro administrador que lo haga.");

                // Serializable: dos administradores degradándose mutuamente al mismo
                // tiempo pasarían los dos el conteo de "queda otro activo" con
                // Read Committed, y el sistema quedaría sin administradores.
                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable);

                var usuario = await ObtenerOFallarAsync(dto.UsuarioId);
                var rolesActuales = await _userManager.GetRolesAsync(usuario);
                var rolActual = rolesActuales.FirstOrDefault();

                if (rolActual == dto.NuevoRol)
                    throw new ValidationException($"El usuario ya tiene el rol {dto.NuevoRol}.");

                if (rolActual == RolesSistema.Administrador && usuario.Estado)
                    await ExigirOtroAdministradorActivoAsync(usuario.Id);

                if (rolesActuales.Count > 0)
                    Exigir(await _userManager.RemoveFromRolesAsync(usuario, rolesActuales));

                Exigir(await _userManager.AddToRoleAsync(usuario, dto.NuevoRol));

                // Las revocaciones eran recortes sobre el rol anterior: no se heredan.
                // Directo sobre el contexto y no vía IPermisosRepository, para que
                // quede dentro de esta misma transacción.
                await _context.PermisosRevocados
                    .Where(p => p.UsuarioId == usuario.Id)
                    .ExecuteDeleteAsync();

                // Cierra la sesión activa del usuario: sus claims de rol y permisos
                // ya no valen (ver RevalidacionSesionProvider).
                Exigir(await _userManager.UpdateSecurityStampAsync(usuario));

                await transaccion.CommitAsync();

                await _bitacora.RegistrarAsync(AccionesBitacora.CambiarRol, ModulosSistema.Seguridad,
                    $"Usuario {usuario.Nombre} ({usuario.Email}): {rolActual ?? "sin rol"} -> {dto.NuevoRol}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al cambiar el rol del usuario.", ex);
            }
        }

        // ------------------------------------------------------------------
        // Estado
        // ------------------------------------------------------------------

        public async Task ActivarUsuarioAsync(string usuarioId)
        {
            try
            {
                var usuario = await ObtenerOFallarAsync(usuarioId);

                if (usuario.Estado)
                    return;

                usuario.Estado = true;

                // Se levanta el bloqueo de la desactivación y también cualquier
                // bloqueo temporal: reactivar es dar acceso, sin condiciones.
                usuario.LockoutEnd = null;
                Exigir(await _userManager.UpdateAsync(usuario));
                Exigir(await _userManager.ResetAccessFailedCountAsync(usuario));

                await _bitacora.RegistrarAsync(AccionesBitacora.Activar, ModulosSistema.Seguridad,
                    $"Usuario {usuario.Nombre} ({usuario.Email})");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al activar el usuario.", ex);
            }
        }

        public async Task DesactivarUsuarioAsync(string usuarioId)
        {
            try
            {
                await ExigirQueNoSeaElMismoAsync(usuarioId,
                    "No podés desactivar tu propio usuario. Pedile a otro administrador que lo haga.");

                await using var transaccion = await _context.Database
                    .BeginTransactionAsync(IsolationLevel.Serializable);

                var usuario = await ObtenerOFallarAsync(usuarioId);

                if (!usuario.Estado)
                    throw new ValidationException("El usuario ya está desactivado.");

                if (await _userManager.IsInRoleAsync(usuario, RolesSistema.Administrador))
                    await ExigirOtroAdministradorActivoAsync(usuario.Id);

                // Estado es lo que consulta el sistema; LockoutEnd en MaxValue hace
                // que Identity rechace el login por su cuenta (cinturón y tirantes).
                usuario.Estado = false;
                usuario.LockoutEnd = DateTimeOffset.MaxValue;
                Exigir(await _userManager.UpdateAsync(usuario));

                // Cierra la sesión activa en menos de un minuto.
                Exigir(await _userManager.UpdateSecurityStampAsync(usuario));

                await transaccion.CommitAsync();

                await _bitacora.RegistrarAsync(AccionesBitacora.Desactivar, ModulosSistema.Seguridad,
                    $"Usuario {usuario.Nombre} ({usuario.Email})");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al desactivar el usuario.", ex);
            }
        }

        // ------------------------------------------------------------------
        // Contraseñas
        // ------------------------------------------------------------------

        public async Task CambiarPasswordAsync(CambioPasswordDto dto)
        {
            try
            {
                var actual = await _usuarioActual.ObtenerAsync()
                    ?? throw new ValidationException("No hay una sesión abierta.");

                if (string.IsNullOrEmpty(dto.PasswordActual))
                    throw new ValidationException("La contraseña actual es obligatoria.");

                UsuarioValidator.ValidarPasswordNueva(dto.PasswordNueva, dto.ConfirmarPassword);

                if (string.Equals(dto.PasswordActual, dto.PasswordNueva, StringComparison.Ordinal))
                    throw new ValidationException("La contraseña nueva tiene que ser distinta de la actual.");

                var usuario = await ObtenerOFallarAsync(actual.Id);

                // ChangePasswordAsync ya actualiza el security stamp: la sesión se
                // cierra y hay que volver a entrar con la contraseña nueva.
                Exigir(await _userManager.ChangePasswordAsync(usuario, dto.PasswordActual, dto.PasswordNueva));

                await _bitacora.RegistrarAsync(AccionesBitacora.CambiarPassword, ModulosSistema.Seguridad,
                    "Cambio de la propia contraseña");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al cambiar la contraseña.", ex);
            }
        }

        public async Task RestablecerPasswordAsync(RestablecerPasswordDto dto)
        {
            try
            {
                await ExigirQueNoSeaElMismoAsync(dto.UsuarioId,
                    "Para cambiar tu propia contraseña usá la opción de Configuración.");

                UsuarioValidator.ValidarPasswordNueva(dto.PasswordTemporal, dto.ConfirmarPassword);

                var usuario = await ObtenerOFallarAsync(dto.UsuarioId);

                // Sin correo no hay flujo de "olvidé mi contraseña": el administrador
                // genera y consume el token en el mismo paso.
                var token = await _userManager.GeneratePasswordResetTokenAsync(usuario);
                Exigir(await _userManager.ResetPasswordAsync(usuario, token, dto.PasswordTemporal));

                await _bitacora.RegistrarAsync(AccionesBitacora.RestablecerPassword, ModulosSistema.Seguridad,
                    $"Contraseña temporal asignada a {usuario.Nombre} ({usuario.Email})");

                // Si el motivo del restablecimiento fue un bloqueo por intentos
                // fallidos, se levanta acá mismo. No toca a un usuario desactivado:
                // eso es decisión aparte (ActivarUsuarioAsync).
                if (usuario.Estado)
                {
                    usuario.LockoutEnd = null;
                    Exigir(await _userManager.UpdateAsync(usuario));
                    Exigir(await _userManager.ResetAccessFailedCountAsync(usuario));
                }
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al restablecer la contraseña.", ex);
            }
        }

        // ------------------------------------------------------------------
        // Permisos por usuario
        // ------------------------------------------------------------------

        public async Task<IReadOnlyList<PermisoUsuarioDto>> ObtenerPermisosAsync(string usuarioId)
        {
            try
            {
                var usuario = await ObtenerOFallarAsync(usuarioId);
                var rol = (await _userManager.GetRolesAsync(usuario)).FirstOrDefault();

                var delRol = new HashSet<string>(PermisosPorRol.Obtener(rol), StringComparer.Ordinal);
                var efectivos = new HashSet<string>(
                    PermisosPorRol.CalcularEfectivos(rol, await _permisos.ObtenerRevocadosAsync(usuario.Id)),
                    StringComparer.Ordinal);

                // Solo los permisos que el rol incluye: un switch para algo que el rol
                // no tiene no se podría encender nunca y confundiría.
                return Permisos.Definiciones
                    .Where(p => delRol.Contains(p.Clave))
                    .Select(p => new PermisoUsuarioDto
                    {
                        Permiso = p.Clave,
                        Modulo = p.Modulo,
                        Descripcion = p.Descripcion,
                        Habilitado = efectivos.Contains(p.Clave)
                    })
                    .ToList();
            }
            catch (Exception ex) when (ex is not NotFoundException)
            {
                throw new Exception("Error al consultar los permisos del usuario.", ex);
            }
        }

        public async Task ActualizarPermisosAsync(ActualizarPermisosDto dto)
        {
            try
            {
                var usuario = await ObtenerOFallarAsync(dto.UsuarioId);
                var rol = (await _userManager.GetRolesAsync(usuario)).FirstOrDefault();

                if (!PermisosPorRol.AdmiteRevocaciones(rol))
                    throw new ValidationException(
                        "Los permisos de un Administrador no se pueden recortar. Si necesita menos acceso, cambiale el rol.");

                var revocados = (dto.PermisosRevocados ?? new List<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var desconocidos = revocados.Where(p => !Permisos.EsValido(p)).ToList();
                if (desconocidos.Count > 0)
                    throw new ValidationException("Hay permisos que no existen: " + string.Join(", ", desconocidos) + ".");

                // Se rechaza la petición entera y no se filtra en silencio: si la
                // pantalla mandó algo fuera del rol, algo está desincronizado.
                var fueraDelRol = PermisosPorRol.RevocacionesInvalidas(rol, revocados);
                if (fueraDelRol.Count > 0)
                    throw new ValidationException(
                        $"El rol {rol} no incluye estos permisos, así que no se pueden quitar: {string.Join(", ", fueraDelRol)}.");

                // Primero el stamp y después las revocaciones: si lo segundo falla,
                // el usuario solo tiene que volver a entrar; al revés, seguiría con
                // claims viejos hasta que venza la cookie.
                Exigir(await _userManager.UpdateSecurityStampAsync(usuario));
                await _permisos.ReemplazarRevocadosAsync(usuario.Id, revocados);

                await _bitacora.RegistrarAsync(AccionesBitacora.CambiarPermisos, ModulosSistema.Seguridad,
                    $"Usuario {usuario.Nombre} ({usuario.Email}): " +
                    (revocados.Count == 0 ? "sin permisos revocados" : "revocados " + string.Join(", ", revocados)));
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al actualizar los permisos del usuario.", ex);
            }
        }

        // ------------------------------------------------------------------
        // Reglas compartidas
        // ------------------------------------------------------------------

        private async Task<UsuarioSigac> ObtenerOFallarAsync(string usuarioId) =>
            await _userManager.FindByIdAsync(usuarioId)
            ?? throw new NotFoundException("El usuario no existe.");

        private async Task ExigirQueNoSeaElMismoAsync(string usuarioId, string mensaje)
        {
            var actual = await _usuarioActual.ObtenerAsync();

            if (actual is not null && actual.Id == usuarioId)
                throw new ValidationException(mensaje);
        }

        // Cuenta los administradores ACTIVOS distintos del afectado, dentro de la
        // transacción abierta por quien llama. Si no queda ninguno, la operación
        // dejaría el sistema sin nadie que pueda gestionar usuarios.
        private async Task ExigirOtroAdministradorActivoAsync(string usuarioIdAfectado)
        {
            var otrosActivos = await (
                from ur in _context.UserRoles
                join r in _context.Roles on ur.RoleId equals r.Id
                join u in _context.Users on ur.UserId equals u.Id
                where r.Name == RolesSistema.Administrador
                      && u.Estado
                      && u.Id != usuarioIdAfectado
                select u.Id).CountAsync();

            if (otrosActivos == 0)
                throw new UltimoAdministradorException(
                    "Es el único administrador activo del sistema. Asigná el rol Administrador " +
                    "a otro usuario activo antes de continuar.");
        }

        // Los mensajes vienen de ErroresIdentityEs, ya en español.
        private static void Exigir(IdentityResult resultado)
        {
            if (resultado.Succeeded)
                return;

            throw new ValidationException(string.Join(" ", resultado.Errors.Select(e => e.Description)));
        }
    }
}
