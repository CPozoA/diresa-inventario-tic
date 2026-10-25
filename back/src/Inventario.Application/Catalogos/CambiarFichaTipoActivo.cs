using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Catalogos;

/// <summary>
/// Cambia qué características técnicas lleva un tipo de activo (D43). Los activos ya registrados conservan sus datos;
/// al editarlos, la ficha muestra solo las secciones del tipo.
/// </summary>
/// <remarks>Requiere Catalogos.GestionarGlobales (solo el superadministrador).</remarks>
public sealed record CambiarFichaTipoActivoCommand(Guid Id, FichaTecnica Ficha) : ICommand<Vacio>;

internal sealed class CambiarFichaTipoActivoHandler(IAppDbContext db) : ICommandHandler<CambiarFichaTipoActivoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CambiarFichaTipoActivoCommand command, CancellationToken cancellationToken)
    {
        var tipo = await db.TiposActivo.FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken);
        if (tipo is null)
        {
            return DefinicionesCatalogo.ElementoNoEncontrado;
        }

        tipo.CambiarFicha(command.Ficha);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}
