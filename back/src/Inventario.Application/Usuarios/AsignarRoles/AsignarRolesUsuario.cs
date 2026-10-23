using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Usuarios.AsignarRoles;

/// <summary>
/// Reemplaza los roles de un usuario. Nadie cambia sus propios roles, nadie otorga permisos que no tiene,
/// y el rol Administrador de sede solo lo da o lo quita el superadministrador.
/// </summary>
/// <remarks>Requiere Usuarios.Gestionar.</remarks>
public sealed record AsignarRolesUsuarioCommand(Guid Id, IReadOnlyList<Guid>? RolIds) : ICommand<Vacio>;

internal sealed class AsignarRolesUsuarioHandler(IAppDbContext db, ReglasDeGestion reglas)
    : ICommandHandler<AsignarRolesUsuarioCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(AsignarRolesUsuarioCommand command, CancellationToken cancellationToken)
    {
        var usuario = await db.BuscarParaGestionAsync(command.Id, cancellationToken);
        if (usuario is null)
        {
            return ErroresUsuario.NoEncontrado;
        }

        var noPuede = await reglas.VerificarPuedeGestionarAsync(usuario, cancellationToken);
        if (noPuede is not null)
        {
            return noPuede;
        }

        var rolIds = (command.RolIds ?? []).Distinct().ToList();
        var roles = await db.Roles
            .Include(r => r.PermisosAsignados)
            .Where(r => rolIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (roles.Count != rolIds.Count)
        {
            return ErroresUsuario.RolNoEncontrado;
        }

        noPuede = await reglas.VerificarPuedeAsignarRolesAsync(roles, cancellationToken);
        if (noPuede is not null)
        {
            return noPuede;
        }

        usuario.AsignarRoles(roles);
        await db.SaveChangesAsync(cancellationToken);

        return Vacio.Valor;
    }
}
