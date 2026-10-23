using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Sedes.Editar;

/// <summary>Cambia el nombre de una sede. El código no cambia: lo usan los reportes y los códigos patrimoniales.</summary>
/// <remarks>Requiere Sedes.Gestionar (solo el superadministrador).</remarks>
public sealed record EditarSedeCommand(Guid Id, string Nombre) : ICommand<Vacio>;

internal sealed class EditarSedeHandler(IAppDbContext db) : ICommandHandler<EditarSedeCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EditarSedeCommand command, CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);
        if (sede is null)
        {
            return ErroresSede.NoEncontrada;
        }

        sede.CambiarNombre(command.Nombre);
        await db.SaveChangesAsync(cancellationToken);

        return Vacio.Valor;
    }
}
