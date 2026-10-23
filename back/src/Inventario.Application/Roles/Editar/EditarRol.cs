using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Roles.Editar;

/// <summary>
/// Cambia el nombre, la descripción y los permisos de un rol de sede.
/// Quien edita no puede tocar un rol que tenga permisos que él no tiene, ni dejarle permisos que él no tiene.
/// </summary>
/// <remarks>Requiere Roles.Gestionar.</remarks>
public sealed record EditarRolCommand(Guid Id, string Nombre, string? Descripcion, IReadOnlyList<string>? Permisos)
    : ICommand<Vacio>;

internal sealed class EditarRolHandler(IAppDbContext db, ReglasDeGestion reglas) : ICommandHandler<EditarRolCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EditarRolCommand command, CancellationToken cancellationToken)
    {
        // El filtro de sede hace que un rol de otra sede "no exista" (404).
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

        var permisos = command.Permisos ?? [];

        var noPuede = await reglas.VerificarPuedeOtorgarPermisosAsync(permisos, cancellationToken);
        if (noPuede is not null)
        {
            return noPuede;
        }

        rol.CambiarDatos(command.Nombre, command.Descripcion);
        rol.DefinirPermisos(permisos);

        if (await db.Roles.AnyAsync(r => r.Id != rol.Id && r.SedeId == rol.SedeId && r.Nombre == rol.Nombre, cancellationToken))
        {
            return ErroresRol.NombreDuplicado;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}
