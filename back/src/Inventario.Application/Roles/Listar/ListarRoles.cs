using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Roles.Listar;

/// <summary>Rol tal como lo ve la pantalla de roles.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Nombre">Nombre visible.</param>
/// <param name="Descripcion">Descripción opcional.</param>
/// <param name="EsDelSistema">Verdadero para Administrador de sede: no se edita ni se elimina.</param>
/// <param name="Permisos">Códigos de permiso que otorga.</param>
/// <param name="CantidadUsuarios">Usuarios de la sede que tienen este rol.</param>
public sealed record RolDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    bool EsDelSistema,
    IReadOnlyList<string> Permisos,
    int CantidadUsuarios);

/// <summary>
/// Roles que se pueden asignar en una sede: los propios de la sede y el de Administrador de sede.
/// El de Superadministrador nunca aparece.
/// </summary>
/// <param name="SedeId">Obligatorio para el superadministrador; los demás solo ven su sede.</param>
/// <remarks>Requiere Roles.Ver.</remarks>
public sealed record ListarRolesQuery(Guid? SedeId) : IQuery<IReadOnlyList<RolDto>>;

internal sealed class ListarRolesHandler(IAppDbContext db, ReglasDeGestion reglas)
    : IQueryHandler<ListarRolesQuery, IReadOnlyList<RolDto>>
{
    public async Task<Result<IReadOnlyList<RolDto>>> Handle(ListarRolesQuery query, CancellationToken cancellationToken)
    {
        var sede = await reglas.ResolverSedeAsync(query.SedeId, cancellationToken);
        if (!sede.EsExito)
        {
            return Result.Fallo<IReadOnlyList<RolDto>>(sede.Error!);
        }

        var sedeId = sede.Valor;

        var roles = await db.Roles
            .AsNoTracking()
            .Include(r => r.PermisosAsignados)
            .Where(r => r.SedeId == sedeId || r.Tipo == TipoRol.AdministradorSede)
            .ToListAsync(cancellationToken);

        var usosPorRol = await db.Usuarios
            .Where(u => u.SedeId == sedeId)
            .SelectMany(u => u.Roles)
            .GroupBy(ur => ur.RolId)
            .Select(g => new { RolId = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancellationToken);

        var usos = usosPorRol.ToDictionary(u => u.RolId, u => u.Cantidad);

        var lista = roles
            .OrderBy(r => r.EsDelSistema ? 0 : 1)
            .ThenBy(r => r.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Select(r => new RolDto(
                r.Id,
                r.Nombre,
                r.Descripcion,
                r.EsDelSistema,
                [.. r.PermisosEfectivos().Order(StringComparer.Ordinal)],
                usos.GetValueOrDefault(r.Id)))
            .ToList();

        return Result.Exito<IReadOnlyList<RolDto>>(lista);
    }
}
