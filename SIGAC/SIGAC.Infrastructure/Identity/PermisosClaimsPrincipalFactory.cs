using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;

namespace SIGAC.Infrastructure.Identity
{
    // Arma el ClaimsPrincipal que Identity mete en la cookie al iniciar sesión.
    // Además de los claims estándar (Id, correo, rol, security stamp) agrega el
    // nombre para mostrar y un claim por cada permiso efectivo, calculado con
    // PermisosPorRol a partir del rol y de las revocaciones guardadas.
    //
    // Los permisos viven en la cookie y no se consultan en cada request: cuando
    // el administrador cambia el rol, los permisos o el estado de un usuario, el
    // servicio actualiza su security stamp, la revalidación detecta la
    // diferencia y cierra la sesión. Al volver a entrar, esta clase vuelve a
    // calcular todo. Así "cambio inmediato" y "sin consultas por request" no se
    // contradicen.
    public class PermisosClaimsPrincipalFactory : UserClaimsPrincipalFactory<UsuarioSigac, IdentityRole>
    {
        private readonly IPermisosRepository _permisos;

        public PermisosClaimsPrincipalFactory(
            UserManager<UsuarioSigac> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> options,
            IPermisosRepository permisos)
            : base(userManager, roleManager, options)
        {
            _permisos = permisos;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UsuarioSigac usuario)
        {
            // La base agrega NameIdentifier, Name, SecurityStamp y un claim de rol
            // por cada rol del usuario.
            var identity = await base.GenerateClaimsAsync(usuario);

            identity.AddClaim(new Claim(ClaimsSigac.Nombre, usuario.Nombre));

            // Un usuario tiene exactamente un rol en SIGAC (CambiarRolAsync quita
            // el anterior antes de poner el nuevo). Si por una edición externa
            // tuviera varios, se toma el primero y no la unión: sumar permisos por
            // accidente es peor que quedarse corto.
            var rol = identity.FindFirst(Options.ClaimsIdentity.RoleClaimType)?.Value;

            var revocados = await _permisos.ObtenerRevocadosAsync(usuario.Id);

            foreach (var permiso in PermisosPorRol.CalcularEfectivos(rol, revocados))
                identity.AddClaim(new Claim(ClaimsSigac.Permiso, permiso));

            return identity;
        }
    }
}
