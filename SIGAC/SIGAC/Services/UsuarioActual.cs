using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SIGAC.Application.Interfaces;
using SIGAC.Infrastructure.Identity;

namespace SIGAC.Services;

// Implementación de IUsuarioActual para Blazor: lee los claims de la cookie a
// través de AuthenticationStateProvider, que funciona tanto en render estático
// (toma HttpContext.User) como dentro del circuito interactivo (donde
// IHttpContextAccessor no es confiable). No consulta la base: todo lo que
// necesita ya viene en la cookie (ver PermisosClaimsPrincipalFactory).
public sealed class UsuarioActual : IUsuarioActual
{
    private readonly AuthenticationStateProvider _estado;

    public UsuarioActual(AuthenticationStateProvider estado)
    {
        _estado = estado;
    }

    public async Task<DatosUsuarioActual?> ObtenerAsync()
    {
        var usuario = (await _estado.GetAuthenticationStateAsync()).User;

        if (usuario.Identity?.IsAuthenticated != true)
            return null;

        var id = usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id))
            return null;

        return new DatosUsuarioActual(
            id,
            usuario.FindFirstValue(ClaimsSigac.Nombre) ?? string.Empty,
            usuario.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            usuario.FindFirstValue(ClaimTypes.Role));
    }
}
