using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Sedes.Editar;

/// <summary>Cambia el nombre de una sede. El código no cambia: lo usan los reportes y los códigos patrimoniales.</summary>
/// <remarks>Requiere Sedes.Gestionar (solo el superadministrador).</remarks>
public sealed record EditarSedeCommand(Guid Id, string Nombre) : ICommand<Vacio>;

internal sealed class EditarSedeHandler(IAppDbContext db, IAuditoria auditoria) : ICommandHandler<EditarSedeCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EditarSedeCommand command, CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);
        if (sede is null)
        {
            return ErroresSede.NoEncontrada;
        }

        var anterior = sede.Nombre;
        sede.CambiarNombre(command.Nombre);
        auditoria.Registrar(ModuloAuditoria.Sedes, $"Cambió el nombre de la sede {anterior} a {sede.Nombre}", sede.Id);
        await db.SaveChangesAsync(cancellationToken);

        return Vacio.Valor;
    }
}
