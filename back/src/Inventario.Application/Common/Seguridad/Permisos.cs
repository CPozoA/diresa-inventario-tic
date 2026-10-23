using Inventario.Application.Common.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Common.Seguridad;

/// <summary>Permisos efectivos del usuario de la petición actual.</summary>
public interface IPermisosUsuarioActual
{
    Task<IReadOnlySet<string>> ObtenerAsync(CancellationToken cancellationToken);

    Task<bool> TieneAsync(string codigoPermiso, CancellationToken cancellationToken);
}

/// <summary>
/// Calcula los permisos efectivos de un usuario: la unión de los permisos de todos sus roles (D16).
/// Un usuario inactivo no tiene permisos. Se consulta en cada petición, así que quitar un permiso
/// o un rol tiene efecto inmediato (docs/ARQUITECTURA.md §6.2).
/// </summary>
internal sealed class CalculadoraPermisos(IAppDbContext db)
{
    private static readonly IReadOnlySet<string> Ninguno = new HashSet<string>();

    public async Task<IReadOnlySet<string>> CalcularAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        // Excepción revisada al filtro de sede: se busca por el identificador de la sesión, para cualquier usuario.
        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        if (usuario is null || !usuario.Activo)
        {
            return Ninguno;
        }

        var rolIds = usuario.Roles.Select(r => r.RolId).ToList();

        // Excepción revisada: los roles ya vienen acotados por los asignados al usuario.
        var roles = await db.Roles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(r => r.PermisosAsignados)
            .Where(r => rolIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        return roles
            .SelectMany(r => r.PermisosEfectivos())
            .ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>Permisos del usuario actual. Se calculan una sola vez por petición.</summary>
internal sealed class PermisosUsuarioActual(IUsuarioActual usuarioActual, CalculadoraPermisos calculadora)
    : IPermisosUsuarioActual
{
    private static readonly IReadOnlySet<string> Ninguno = new HashSet<string>();

    private IReadOnlySet<string>? _permisos;

    public async Task<IReadOnlySet<string>> ObtenerAsync(CancellationToken cancellationToken)
    {
        if (_permisos is not null)
        {
            return _permisos;
        }

        _permisos = usuarioActual.EstaAutenticado && usuarioActual.UsuarioId is Guid usuarioId
            ? await calculadora.CalcularAsync(usuarioId, cancellationToken)
            : Ninguno;

        return _permisos;
    }

    public async Task<bool> TieneAsync(string codigoPermiso, CancellationToken cancellationToken)
    {
        var permisos = await ObtenerAsync(cancellationToken);
        return permisos.Contains(codigoPermiso);
    }
}
