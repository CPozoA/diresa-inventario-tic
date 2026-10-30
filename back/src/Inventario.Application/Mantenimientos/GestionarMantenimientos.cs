using Inventario.Application.Activos;
using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Auditoria;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Mantenimientos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Mantenimientos;

/// <summary>
/// Registra un mantenimiento (D53). Abierto, el activo pasa a un estado "en mantenimiento". Con <paramref name="Cierre"/>
/// se registra ya terminado (por ejemplo, un preventivo del mismo día) y el activo queda como indique el cierre.
/// </summary>
/// <param name="ActivoId">Activo (no de baja, sin otro mantenimiento abierto).</param>
/// <param name="Tipo">Preventivo o correctivo.</param>
/// <param name="FechaInicio">No posterior a hoy.</param>
/// <param name="Detalle">Problema reportado (obligatorio en un correctivo) o actividades previstas.</param>
/// <param name="Ejecutor">Personal de TI (con técnico) o proveedor (con empresa, orden de servicio y costo).</param>
/// <param name="Cierre">Si ya terminó.</param>
/// <remarks>Requiere Mantenimiento.Gestionar.</remarks>
public sealed record RegistrarMantenimientoCommand(
    Guid ActivoId,
    TipoMantenimiento Tipo,
    DateOnly FechaInicio,
    string? Detalle,
    DatosEjecutor Ejecutor,
    CierreMantenimiento? Cierre) : ICommand<IdCreado>;

/// <summary>Cierra un mantenimiento: cómo quedó, en qué estado y conservación queda el activo y su próximo preventivo.</summary>
/// <remarks>Requiere Mantenimiento.Gestionar.</remarks>
public sealed record CerrarMantenimientoCommand(Guid Id, CierreMantenimiento Cierre) : ICommand<Vacio>;

internal sealed class RegistrarMantenimientoHandler(IAppDbContext db, TimeProvider reloj, AplicadorCierre aplicador, IAuditoria auditoria)
    : ICommandHandler<RegistrarMantenimientoCommand, IdCreado>
{
    private const string EstadoPreferido = "En mantenimiento";

    public async Task<Result<IdCreado>> Handle(RegistrarMantenimientoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command.Ejecutor);

        // El filtro de sede hace que un activo de otra red "no exista".
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
            return ErroresMantenimiento.ActivoDeBaja;
        }

        if (await db.Mantenimientos.AnyAsync(m => m.ActivoId == activo.Id && m.FechaFin == null, cancellationToken))
        {
            return ErroresMantenimiento.YaHayUnoAbierto;
        }

        if (command.Ejecutor.Ejecutor == EjecutorMantenimiento.PersonalTi
            && (command.Ejecutor.TecnicoId is not Guid tecnicoId
                || !await ConsultasProgramados.Tecnicos(db, activo.SedeId).AnyAsync(u => u.Id == tecnicoId, cancellationToken)))
        {
            return ErroresMantenimiento.TecnicoInvalido;
        }

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (command.FechaInicio > hoy)
        {
            return ErroresMantenimiento.FechaFutura;
        }

        var mantenimiento = Mantenimiento.Abrir(
            activo.SedeId, activo.Id, command.Tipo, command.FechaInicio, command.Detalle, command.Ejecutor, hoy);

        if (command.Cierre is CierreMantenimiento cierre)
        {
            var aplicado = await aplicador.AplicarAsync(mantenimiento, activo, cierre, hoy, cancellationToken);
            if (!aplicado.EsExito)
            {
                return aplicado.Error!;
            }
        }
        else if (await EstadoEnMantenimientoAsync(cancellationToken) is Guid enMantenimiento)
        {
            activo.CambiarSituacion(enMantenimiento, activo.Conservacion);
        }

        db.Mantenimientos.Add(mantenimiento);

        // Si dos personas lo registran a la vez, el índice único del mantenimiento abierto lo impide.
        var tipoTexto = command.Tipo == TipoMantenimiento.Preventivo ? "preventivo" : "correctivo";
        auditoria.Registrar(
            ModuloAuditoria.Mantenimiento,
            command.Cierre is null
                ? $"Registró un mantenimiento {tipoTexto} de {activo.CodigoPatrimonial}"
                : $"Registró un mantenimiento {tipoTexto} ya terminado de {activo.CodigoPatrimonial}",
            activo.SedeId,
            activo.Id);
        await db.SaveChangesAsync(cancellationToken);
        return new IdCreado(mantenimiento.Id);
    }

    /// <summary>Un estado activo del grupo "en mantenimiento": "En mantenimiento" si existe. Nulo si no hay.</summary>
    private async Task<Guid?> EstadoEnMantenimientoAsync(CancellationToken cancellationToken) =>
        await db.EstadosActivo.AsNoTracking()
            .Where(e => e.Activo && e.GrupoAsignado == GrupoEstado.EnMantenimiento)
            .OrderBy(e => e.Nombre == EstadoPreferido ? 0 : 1)
            .ThenBy(e => e.Nombre)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
}

internal sealed class CerrarMantenimientoHandler(IAppDbContext db, TimeProvider reloj, AplicadorCierre aplicador, IAuditoria auditoria)
    : ICommandHandler<CerrarMantenimientoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CerrarMantenimientoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command.Cierre);

        var mantenimiento = await db.Mantenimientos.FirstOrDefaultAsync(m => m.Id == command.Id, cancellationToken);
        if (mantenimiento is null)
        {
            return ErroresMantenimiento.NoEncontrado;
        }

        if (!mantenimiento.Abierto)
        {
            return ErroresMantenimiento.YaCerrado;
        }

        var activo = await db.Activos.Where(a => a.Id == mantenimiento.ActivoId).FirstAsync(cancellationToken);
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

        var aplicado = await aplicador.AplicarAsync(mantenimiento, activo, command.Cierre, hoy, cancellationToken);
        if (!aplicado.EsExito)
        {
            return aplicado.Error!;
        }

        auditoria.Registrar(
            ModuloAuditoria.Mantenimiento,
            command.Cierre.Resultado == ResultadoMantenimiento.SinReparacion
                ? $"Cerró el mantenimiento de {activo.CodigoPatrimonial} (sin reparación)"
                : $"Cerró el mantenimiento de {activo.CodigoPatrimonial}",
            activo.SedeId,
            activo.Id);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

/// <summary>Cierra el mantenimiento y deja el activo con el estado, la conservación y el próximo preventivo indicados.</summary>
internal sealed class AplicadorCierre(IAppDbContext db)
{
    public async Task<Result<Vacio>> AplicarAsync(
        Mantenimiento mantenimiento, Activo activo, CierreMantenimiento cierre, DateOnly hoy, CancellationToken cancellationToken)
    {
        if (cierre.FechaFin > hoy)
        {
            return ErroresMantenimiento.FechaFutura;
        }

        if (!await db.EstadosActivo.AnyAsync(e => e.Id == cierre.EstadoActivoId && e.Activo, cancellationToken))
        {
            return ErroresMantenimiento.EstadoInvalido;
        }

        mantenimiento.Cerrar(new DatosCierre(cierre.FechaFin, cierre.TrabajoRealizado, cierre.Resultado, cierre.Costo), hoy);
        activo.CambiarSituacion(cierre.EstadoActivoId, cierre.Conservacion);
        if (cierre.ProximoMantenimiento != activo.ProximoMantenimiento)
        {
            // Solo se valida si cambia: un correctivo puede dejar pendiente el preventivo que ya estaba vencido.
            activo.ProgramarMantenimiento(cierre.ProximoMantenimiento, cierre.FechaFin);
        }
        return Vacio.Valor;
    }
}
