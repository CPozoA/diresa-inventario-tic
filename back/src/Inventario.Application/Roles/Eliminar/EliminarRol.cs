using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Roles.Eliminar;

/// <summary>Elimina un rol de sede que no esté asignado a nadie.</summary>
/// <remarks>Requiere Roles.Gestionar.</remarks>
public sealed record EliminarRolCommand(Guid Id) : ICommand<Vacio>;

internal sealed class EliminarRolHandler(IAppDbContext db, ReglasDeGestion reglas) : ICommandHandler<EliminarRolCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EliminarRolCommand command, CancellationToken cancellationToken)
    {
        var rol = await db.Roles
            .Include(r => r.PermisosAsignados)
            .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken);

        if (rol is null)
        {
            return ErroresRol.NoEncontrado;
        }

        if (rol.EsDelSistema)
        {
            return ErroresRol.DelSistema;
        }

        if (await reglas.VerificarPuedeOtorgarPermisosAsync(rol.PermisosEfectivos(), cancellationToken) is not null)
        {
            return ErroresRol.ConMasPermisos;
        }

        // Excepción revisada al filtro de sede: el rol ya es visible para quien elimina; se cuentan todos sus usos
        // (un rol de sede solo puede estar asignado a usuarios de esa misma sede).
        var enUso = await db.Usuarios
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Roles.Any(ur => ur.RolId == rol.Id), cancellationToken);

        if (enUso)
        {
            return ErroresRol.EnUso;
        }

        db.Roles.Remove(rol);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}
