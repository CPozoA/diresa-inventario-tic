using System.Security.Claims;
using Inventario.Application.Common.Seguridad;

namespace Inventario.Api.Seguridad;

/// <summary>Nombres de los datos (claims) que la sesión guarda del usuario.</summary>
internal static class TiposClaim
{
    public const string Sede = "inventario:sede";
    public const string SuperAdmin = "inventario:superadmin";
    public const string Permiso = "inventario:permiso";
}

/// <summary>
/// Lee el usuario de la sesión HTTP. Fuera de una petición (por ejemplo, al cargar datos iniciales) no hay usuario.
/// En el Paso 3 la sesión empieza a llevar estos datos; en el Paso 4 los permisos se resuelven en el servidor en cada petición.
/// </summary>
internal sealed class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    private ClaimsPrincipal? Principal => accesor.HttpContext?.User;

    public bool EstaAutenticado => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UsuarioId => LeerGuid(ClaimTypes.NameIdentifier);

    public Guid? SedeId => LeerGuid(TiposClaim.Sede);

    // Se exige un dato explícito: nunca se deduce "superadmin" por la ausencia de sede.
    public bool EsSuperAdmin => EstaAutenticado && Principal!.HasClaim(TiposClaim.SuperAdmin, bool.TrueString);

    public bool TienePermiso(string codigoPermiso) =>
        EstaAutenticado && (EsSuperAdmin || Principal!.HasClaim(TiposClaim.Permiso, codigoPermiso));

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
