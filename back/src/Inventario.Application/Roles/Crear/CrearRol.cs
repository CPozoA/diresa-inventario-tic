using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;
using Inventario.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Roles.Crear;

/// <summary>Crea un rol propio de una sede.</summary>
/// <param name="SedeId">Obligatorio para el superadministrador; los demás crean en su sede.</param>
/// <param name="Nombre">Único dentro de la sede.</param>
/// <param name="Descripcion">Opcional.</param>
/// <param name="Permisos">Solo permisos que quien crea ya tiene.</param>
/// <remarks>Requiere Roles.Gestionar.</remarks>
public sealed record CrearRolCommand(Guid? SedeId, string Nombre, string? Descripcion, IReadOnlyList<string>? Permisos)
    : ICommand<IdCreado>;

internal sealed class CrearRolHandler(IAppDbContext db, ReglasDeGestion reglas, IAuditoria auditoria) : ICommandHandler<CrearRolCommand, IdCreado>
{
    public async Task<Result<IdCreado>> Handle(CrearRolCommand command, CancellationToken cancellationToken)
    {
        var sede = await reglas.ResolverSedeAsync(command.SedeId, cancellationToken);
        if (!sede.EsExito)
        {
            return sede.Error!;
        }

        var permisos = command.Permisos ?? [];

        var noPuede = await reglas.VerificarPuedeOtorgarPermisosAsync(permisos, cancellationToken);
        if (noPuede is not null)
        {
            return noPuede;
        }

        // El dominio valida el nombre, que los permisos existan y que ninguno sea exclusivo del superadministrador.
        var rol = Rol.CrearDeSede(sede.Valor, command.Nombre, command.Descripcion, permisos);

        if (await db.Roles.AnyAsync(r => r.SedeId == rol.SedeId && r.Nombre == rol.Nombre, cancellationToken))
        {
            return ErroresRol.NombreDuplicado;
        }

        db.Roles.Add(rol);
        auditoria.Registrar(ModuloAuditoria.Roles, $"Creó el rol {rol.Nombre}", rol.SedeId);
        await db.SaveChangesAsync(cancellationToken);

        return new IdCreado(rol.Id);
    }
}
