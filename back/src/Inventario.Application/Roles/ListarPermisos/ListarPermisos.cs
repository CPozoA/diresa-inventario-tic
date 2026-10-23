using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Seguridad;

namespace Inventario.Application.Roles.ListarPermisos;

/// <summary>Permiso que se puede marcar al crear o editar un rol de sede.</summary>
/// <param name="Codigo">Código estable, por ejemplo <c>Activos.Crear</c>.</param>
/// <param name="Modulo">Grupo en la pantalla.</param>
/// <param name="Descripcion">Texto para el administrador.</param>
/// <param name="PuedeOtorgar">Falso si quien consulta no tiene ese permiso (no puede otorgarlo).</param>
public sealed record PermisoDto(string Codigo, string Modulo, string Descripcion, bool PuedeOtorgar);

/// <summary>Catálogo de permisos asignables a roles de sede (sin los exclusivos del superadministrador).</summary>
/// <remarks>Requiere Roles.Ver.</remarks>
public sealed record ListarPermisosQuery : IQuery<IReadOnlyList<PermisoDto>>;

internal sealed class ListarPermisosHandler(IUsuarioActual actual, IPermisosUsuarioActual permisos)
    : IQueryHandler<ListarPermisosQuery, IReadOnlyList<PermisoDto>>
{
    public async Task<Result<IReadOnlyList<PermisoDto>>> Handle(ListarPermisosQuery query, CancellationToken cancellationToken)
    {
        var propios = await permisos.ObtenerAsync(cancellationToken);

        var lista = Permisos.Todos
            .Where(p => !p.ExclusivoSuperAdmin)
            .Select(p => new PermisoDto(p.Codigo, p.Modulo, p.Descripcion, actual.EsSuperAdmin || propios.Contains(p.Codigo)))
            .ToList();

        return Result.Exito<IReadOnlyList<PermisoDto>>(lista);
    }
}
