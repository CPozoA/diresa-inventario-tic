using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Sedes.CambiarEstado;

/// <summary>
/// Activa o desactiva una sede. Una sede inactiva conserva sus datos, pero sus usuarios no pueden ingresar:
/// al desactivarla se cierran las sesiones abiertas de todos sus usuarios.
/// </summary>
/// <remarks>Requiere Sedes.Gestionar (solo el superadministrador).</remarks>
public sealed record CambiarEstadoSedeCommand(Guid Id, bool Activa) : ICommand<Vacio>;

internal sealed class CambiarEstadoSedeHandler(IAppDbContext db, IServicioIdentidad identidad, IAuditoria auditoria)
    : ICommandHandler<CambiarEstadoSedeCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CambiarEstadoSedeCommand command, CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);
        if (sede is null)
        {
            return ErroresSede.NoEncontrada;
        }

        if (command.Activa)
        {
            sede.Activar();
            auditoria.Registrar(ModuloAuditoria.Sedes, $"Activó la sede {sede.Nombre}", sede.Id);
            await db.SaveChangesAsync(cancellationToken);
            return Vacio.Valor;
        }

        sede.Desactivar();
        auditoria.Registrar(ModuloAuditoria.Sedes, $"Desactivó la sede {sede.Nombre} y cerró las sesiones de sus usuarios", sede.Id);
        await db.SaveChangesAsync(cancellationToken);

        var usuarios = await db.Usuarios
            .Where(u => u.SedeId == sede.Id)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (var usuarioId in usuarios)
        {
            await identidad.InvalidarSesionesAsync(usuarioId);
        }

        return Vacio.Valor;
    }
}
