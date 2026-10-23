using System.Security.Claims;
using Inventario.Application.Common.Seguridad;

namespace Inventario.Api.Seguridad;

/// <summary>
/// Lee el usuario de la sesión HTTP (cookie). Fuera de una petición (por ejemplo, al cargar datos iniciales) no hay usuario.
/// </summary>
internal sealed class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    private ClaimsPrincipal? Principal => accesor.HttpContext?.User;

    public bool EstaAutenticado => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UsuarioId => LeerGuid(ClaimTypes.NameIdentifier);

    public Guid? SedeId => LeerGuid(TiposClaim.Sede);

    // Se exige un dato explícito: nunca se deduce "superadmin" por la ausencia de sede.
    public bool EsSuperAdmin => EstaAutenticado && Principal!.HasClaim(TiposClaim.SuperAdmin, bool.TrueString);

    /// <summary>
    /// Por ahora solo el superadministrador tiene permisos. En el Paso 4 los permisos de cada usuario
    /// se calculan en el servidor a partir de sus roles, en cada petición.
    /// </summary>
    public bool TienePermiso(string codigoPermiso) => EsSuperAdmin;

    private Guid? LeerGuid(string tipoClaim)
    {
        if (!EstaAutenticado)
        {
            return null;
        }

        var valor = Principal!.FindFirstValue(tipoClaim);
        return Guid.TryParse(valor, out var id) ? id : null;
    }
}
