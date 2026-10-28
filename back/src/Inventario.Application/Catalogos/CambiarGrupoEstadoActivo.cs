using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Catalogos;

/// <summary>
/// Cambia a qué grupo pertenece un estado del activo (D48): en uso, en mantenimiento, fuera de servicio, en almacén o
/// de baja. El resumen del inicio cuenta por grupo, y los de baja no entran en las alertas (D49).
/// </summary>
/// <remarks>Requiere Catalogos.GestionarGlobales (solo el superadministrador).</remarks>
public sealed record CambiarGrupoEstadoActivoCommand(Guid Id, GrupoEstado Grupo) : ICommand<Vacio>;

internal sealed class CambiarGrupoEstadoActivoHandler(IAppDbContext db) : ICommandHandler<CambiarGrupoEstadoActivoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CambiarGrupoEstadoActivoCommand command, CancellationToken cancellationToken)
    {
        var estado = await db.EstadosActivo.FirstOrDefaultAsync(e => e.Id == command.Id, cancellationToken);
        if (estado is null)
        {
            return DefinicionesCatalogo.ElementoNoEncontrado;
        }

        estado.CambiarGrupo(command.Grupo);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}
