using Inventario.Application.Activos;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Asignaciones;
using Inventario.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Asignaciones;

/// <summary>
/// Entrega el activo a una persona de la misma red (D51). Si ya lo tenía otra persona, esa asignación se cierra con
/// la misma fecha y conservación (reasignación). Si el equipo estaba en almacén, pasa a un estado "en uso".
/// </summary>
/// <param name="ActivoId">Activo.</param>
/// <param name="PersonaId">Persona activa de la misma red.</param>
/// <param name="FechaEntrega">No posterior a hoy ni anterior al último movimiento del equipo.</param>
/// <param name="Conservacion">Estado físico al entregarlo; queda también como conservación del activo.</param>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record AsignarActivoCommand(Guid ActivoId, Guid PersonaId, DateOnly FechaEntrega, Conservacion Conservacion)
    : ICommand<IdCreado>;

/// <summary>Registra que la persona devolvió el activo. El activo queda con el estado y la conservación indicados.</summary>
/// <param name="ActivoId">Activo.</param>
/// <param name="Fecha">No anterior a la entrega ni posterior a hoy.</param>
/// <param name="Conservacion">Estado físico al devolverlo.</param>
/// <param name="EstadoActivoId">Estado del activo después de la devolución (la interfaz propone "En almacén").</param>
/// <param name="Observaciones">Notas ("se devuelve sin cargador").</param>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record DevolverActivoCommand(Guid ActivoId, DateOnly Fecha, Conservacion Conservacion, Guid EstadoActivoId, string? Observaciones)
    : ICommand<Vacio>;

internal sealed class AsignarActivoHandler(IAppDbContext db, TimeProvider reloj) : ICommandHandler<AsignarActivoCommand, IdCreado>
{
    private const string EstadoPreferido = "Operativo";

    public async Task<Result<IdCreado>> Handle(AsignarActivoCommand command, CancellationToken cancellationToken)
    {
        // El filtro de sede hace que un activo o una persona de otra red "no existan".
        var activo = await db.Activos.FirstOrDefaultAsync(a => a.Id == command.ActivoId, cancellationToken);
        if (activo is null)
        {
            return ErroresActivo.NoEncontrado;
        }

        var grupo = await db.EstadosActivo.AsNoTracking()
            .Where(e => e.Id == activo.EstadoActivoId)
            .Select(e => e.GrupoAsignado ?? GrupoEstado.EnUso)
            .FirstAsync(cancellationToken);
        if (grupo == GrupoEstado.DeBaja)
        {
            return ErroresAsignacion.ActivoDeBaja;
        }

        var persona = await db.Personal.AsNoTracking()
            .Where(p => p.Id == command.PersonaId)
            .Select(p => new { p.SedeId, p.Activa })
            .FirstOrDefaultAsync(cancellationToken);
        if (persona is null)
        {
            return ErroresAsignacion.PersonaInvalida;
        }

        if (persona.SedeId != activo.SedeId)
        {
            return ErroresAsignacion.OtraRed;
        }

        if (!persona.Activa)
        {
            return ErroresAsignacion.PersonaCesada;
        }

        if (!Enum.IsDefined(command.Conservacion))
        {
            return ErrorAplicacion.Validacion("Asignaciones.ConservacionInvalida", "Elija el estado de conservación al entregar.");
        }

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (command.FechaEntrega > hoy)
        {
            return ErroresAsignacion.FechaFutura;
        }

        var vigente = await db.Asignaciones.FirstOrDefaultAsync(x => x.ActivoId == activo.Id && x.FechaDevolucion == null, cancellationToken);
        if (vigente?.PersonaId == command.PersonaId)
        {
            return ErroresAsignacion.YaLaTiene;
        }

        if (await ConsultasAsignacion.UltimoMovimientoAsync(db, activo.Id, cancellationToken) is DateOnly ultimo && command.FechaEntrega < ultimo)
        {
            return ErroresAsignacion.FechaAnterior(ultimo);
        }

        // Reasignación: quien lo tenía lo "devuelve" el mismo día y en el mismo estado en que se entrega al nuevo.
        vigente?.Devolver(command.FechaEntrega, command.Conservacion, observaciones: null, hoy);

        var asignacion = Asignacion.Entregar(activo.SedeId, activo.Id, command.PersonaId, command.FechaEntrega, command.Conservacion, hoy);
        db.Asignaciones.Add(asignacion);

        var estado = grupo == GrupoEstado.EnAlmacen ? await EstadoEnUsoAsync(cancellationToken) ?? activo.EstadoActivoId : activo.EstadoActivoId;
        activo.CambiarSituacion(estado, command.Conservacion);

        // Si alguien asignó el mismo equipo a la vez, el índice único de la asignación vigente lo impide.
        await db.SaveChangesAsync(cancellationToken);
        return new IdCreado(asignacion.Id);
    }

    /// <summary>Un estado activo del grupo "en uso": "Operativo" si existe; si no, el primero por nombre. Nulo si no hay.</summary>
    private async Task<Guid?> EstadoEnUsoAsync(CancellationToken cancellationToken) =>
        await db.EstadosActivo.AsNoTracking()
            .Where(e => e.Activo && (e.GrupoAsignado ?? GrupoEstado.EnUso) == GrupoEstado.EnUso)
            .OrderBy(e => e.Nombre == EstadoPreferido ? 0 : 1)
            .ThenBy(e => e.Nombre)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
}

internal sealed class DevolverActivoHandler(IAppDbContext db, TimeProvider reloj) : ICommandHandler<DevolverActivoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(DevolverActivoCommand command, CancellationToken cancellationToken)
    {
        var activo = await db.Activos.FirstOrDefaultAsync(a => a.Id == command.ActivoId, cancellationToken);
        if (activo is null)
        {
            return ErroresActivo.NoEncontrado;
        }

        var vigente = await db.Asignaciones.FirstOrDefaultAsync(x => x.ActivoId == activo.Id && x.FechaDevolucion == null, cancellationToken);
        if (vigente is null)
        {
            return ErroresAsignacion.SinAsignacion;
        }

        if (!await db.EstadosActivo.AnyAsync(e => e.Id == command.EstadoActivoId && e.Activo, cancellationToken))
        {
            return ErroresAsignacion.EstadoInvalido;
        }

        if (!Enum.IsDefined(command.Conservacion))
        {
            return ErrorAplicacion.Validacion("Asignaciones.ConservacionInvalida", "Elija el estado de conservación al devolver.");
        }

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (command.Fecha > hoy)
        {
            return ErroresAsignacion.FechaFutura;
        }

        if (command.Fecha < vigente.FechaEntrega)
        {
            return ErroresAsignacion.FechaAnterior(vigente.FechaEntrega);
        }

        vigente.Devolver(command.Fecha, command.Conservacion, command.Observaciones, hoy);
        activo.CambiarSituacion(command.EstadoActivoId, command.Conservacion);

        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}
