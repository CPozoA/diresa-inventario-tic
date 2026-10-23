using Inventario.Application.Common.Seguridad;
using Microsoft.AspNetCore.Authorization;

namespace Inventario.Api.Seguridad;

/// <summary>Requisito: el usuario debe tener este permiso (por alguno de sus roles).</summary>
internal sealed class PermisoRequirement(string permiso) : IAuthorizationRequirement
{
    public string Permiso { get; } = permiso;
}

/// <summary>Comprueba el permiso con los roles del usuario, consultados en el servidor en cada petición.</summary>
internal sealed class PermisoHandler(IPermisosUsuarioActual permisos) : AuthorizationHandler<PermisoRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        if (await permisos.TieneAsync(requirement.Permiso, CancellationToken.None))
        {
            context.Succeed(requirement);
        }
    }
}

internal static class AutorizacionPorPermiso
{
    /// <summary>
    /// Exige sesión iniciada y el permiso indicado. Sin sesión responde 401; sin el permiso, 403.
    /// Uso: <c>grupo.MapGet("/", Listar).RequirePermiso(Permisos.Sedes.Gestionar);</c>
    /// </summary>
    public static TBuilder RequirePermiso<TBuilder>(this TBuilder builder, string permiso)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermisoRequirement(permiso))
            .Build());
}
