using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SIGAC.Infrastructure.Identity;

namespace SIGAC.Services;

// Un circuito de Blazor Server puede durar horas con la misma cookie. Sin esto,
// un usuario desactivado o con el rol cambiado seguiría operando hasta cerrar
// el navegador. Cada RevalidationInterval se compara el security stamp de la
// cookie con el de la base: si el administrador cambió rol, permisos o estado
// (UsuariosService actualiza el stamp en los tres casos), difieren y Blazor
// cierra la sesión del circuito. Es el mismo IdentityRevalidatingAuthentication
// StateProvider de la plantilla oficial, con el intervalo bajado de 30 minutos
// a 1 para cumplir "el cambio se refleja de inmediato" (PBI 1946).
internal sealed class RevalidacionSesionProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IdentityOptions _opciones;

    public RevalidacionSesionProvider(
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentityOptions> opciones)
        : base(loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones.Value;
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState estado, CancellationToken cancellationToken)
    {
        // Scope propio: el UserManager es scoped y este provider vive tanto como
        // el circuito, así que no se puede inyectar directo.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UsuarioSigac>>();

        return await SigueVigenteAsync(userManager, estado.User);
    }

    private async Task<bool> SigueVigenteAsync(UserManager<UsuarioSigac> userManager, ClaimsPrincipal principal)
    {
        var usuario = await userManager.GetUserAsync(principal);

        if (usuario is null)
            return false;

        // Cinturón y tirantes: desactivar ya cambia el stamp, pero si alguien
        // apagara Estado directo en la base, esto lo saca igual.
        if (!usuario.Estado)
            return false;

        if (!userManager.SupportsUserSecurityStamp)
            return true;

        var stampEnCookie = principal.FindFirstValue(_opciones.ClaimsIdentity.SecurityStampClaimType);
        var stampEnBase = await userManager.GetSecurityStampAsync(usuario);

        return stampEnCookie == stampEnBase;
    }
}
