using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Auditoria;
using Inventario.Domain.Sedes;
using Inventario.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Sedes.Crear;

/// <summary>Da de alta una sede. Nace activa y con los roles iniciales (Editor y Consulta).</summary>
/// <remarks>Requiere Sedes.Gestionar (solo el superadministrador).</remarks>
public sealed record CrearSedeCommand(string Codigo, string Nombre) : ICommand<IdCreado>;

internal sealed class CrearSedeHandler(IAppDbContext db, IAuditoria auditoria) : ICommandHandler<CrearSedeCommand, IdCreado>
{
    public async Task<Result<IdCreado>> Handle(CrearSedeCommand command, CancellationToken cancellationToken)
    {
        // El dominio normaliza el código (mayúsculas) y valida formato y longitudes.
        var sede = Sede.Crear(command.Codigo, command.Nombre);

        if (await db.Sedes.AnyAsync(s => s.Codigo == sede.Codigo, cancellationToken))
        {
            return ErroresSede.CodigoDuplicado;
        }

        db.Sedes.Add(sede);
        db.Roles.AddRange(RolesDelSistema.CrearInicialesDeSede(sede.Id));
        auditoria.Registrar(ModuloAuditoria.Sedes, $"Creó la sede {sede.Nombre} ({sede.Codigo})", sede.Id);
        await db.SaveChangesAsync(cancellationToken);

        return new IdCreado(sede.Id);
    }
}
