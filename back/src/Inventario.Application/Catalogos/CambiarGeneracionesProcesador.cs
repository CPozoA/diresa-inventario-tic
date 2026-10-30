using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Auditoria;
using Inventario.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Catalogos;

/// <summary>
/// Cambia qué generaciones ofrece una familia de procesadores (D44). Los activos ya registrados conservan su dato;
/// al editarlos, si su generación ya no corresponde, se debe elegir otra.
/// </summary>
/// <remarks>Requiere Catalogos.GestionarGlobales (solo el superadministrador).</remarks>
public sealed record CambiarGeneracionesProcesadorCommand(Guid Id, GeneracionesProcesador Generaciones) : ICommand<Vacio>;

internal sealed class CambiarGeneracionesProcesadorHandler(IAppDbContext db, IAuditoria auditoria)
    : ICommandHandler<CambiarGeneracionesProcesadorCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CambiarGeneracionesProcesadorCommand command, CancellationToken cancellationToken)
    {
        var procesador = await db.Procesadores.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (procesador is null)
        {
            return DefinicionesCatalogo.ElementoNoEncontrado;
        }

        procesador.CambiarGeneraciones(command.Generaciones);
        auditoria.Registrar(ModuloAuditoria.Catalogos, $"Cambió las generaciones del procesador {procesador.Nombre}", sedeId: null);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}
