using System.Globalization;
using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Licencias;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Licencias;

/// <summary>Registra una suscripción de software (D54).</summary>
/// <param name="SedeId">Red: el superadministrador la indica; un usuario de sede usa la suya.</param>
/// <param name="Datos">Producto, proveedor, puestos, fechas, documento, costo y observaciones.</param>
/// <remarks>Requiere Licencias.Gestionar.</remarks>
public sealed record RegistrarLicenciaCommand(Guid? SedeId, DatosLicencia Datos) : ICommand<IdCreado>;

/// <summary>Modifica una licencia; también para renovarla (nuevas fechas). Puede dejarla con menos puestos que equipos.</summary>
/// <remarks>Requiere Licencias.Gestionar.</remarks>
public sealed record ActualizarLicenciaCommand(Guid Id, DatosLicencia Datos) : ICommand<Vacio>;

/// <summary>Elimina una licencia registrada por error, con sus equipos.</summary>
/// <remarks>Requiere Licencias.Gestionar.</remarks>
public sealed record EliminarLicenciaCommand(Guid Id) : ICommand<Vacio>;

/// <summary>La licencia no se renovó: deja de contar en las alertas y libera sus equipos.</summary>
/// <remarks>Requiere Licencias.Gestionar.</remarks>
public sealed record ArchivarLicenciaCommand(Guid Id) : ICommand<Vacio>;

/// <summary>Vuelve a activar una licencia archivada (sin equipos).</summary>
/// <remarks>Requiere Licencias.Gestionar.</remarks>
public sealed record ReactivarLicenciaCommand(Guid Id) : ICommand<Vacio>;

/// <summary>Asigna puestos a equipos de la misma red (no de baja). Todos o ninguno.</summary>
/// <remarks>Requiere Licencias.Gestionar.</remarks>
public sealed record AsignarLicenciaCommand(Guid Id, IReadOnlyList<Guid> ActivoIds) : ICommand<Vacio>;

/// <summary>Libera el puesto de un equipo.</summary>
/// <remarks>Requiere Licencias.Gestionar.</remarks>
public sealed record QuitarLicenciaCommand(Guid Id, Guid ActivoId) : ICommand<Vacio>;

internal sealed class RegistrarLicenciaHandler(IAppDbContext db, ReglasDeGestion reglas, IAuditoria auditoria) : ICommandHandler<RegistrarLicenciaCommand, IdCreado>
{
    public async Task<Result<IdCreado>> Handle(RegistrarLicenciaCommand command, CancellationToken cancellationToken)
    {
        var sede = await reglas.ResolverSedeAsync(command.SedeId, cancellationToken);
        if (!sede.EsExito)
        {
            return sede.Error!;
        }

        var licencia = Licencia.Registrar(sede.Valor, command.Datos);
        db.Licencias.Add(licencia);
        auditoria.Registrar(ModuloAuditoria.Licencias, $"Registró la licencia {licencia.Producto} ({licencia.Puestos} puestos)", licencia.SedeId);
        await db.SaveChangesAsync(cancellationToken);
        return new IdCreado(licencia.Id);
    }
}

internal sealed class ActualizarLicenciaHandler(IAppDbContext db, IAuditoria auditoria) : ICommandHandler<ActualizarLicenciaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(ActualizarLicenciaCommand command, CancellationToken cancellationToken)
    {
        var licencia = await db.Licencias.FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken);
        if (licencia is null)
        {
            return ErroresLicencia.NoEncontrada;
        }

        var finAnterior = licencia.FechaFin;
        licencia.Actualizar(command.Datos);
        auditoria.Registrar(
            ModuloAuditoria.Licencias,
            licencia.FechaFin > finAnterior
                ? $"Renovó la licencia {licencia.Producto} hasta el {licencia.FechaFin.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}"
                : $"Editó la licencia {licencia.Producto}",
            licencia.SedeId);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal sealed class EliminarLicenciaHandler(IAppDbContext db, IAuditoria auditoria) : ICommandHandler<EliminarLicenciaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EliminarLicenciaCommand command, CancellationToken cancellationToken)
    {
        var licencia = await db.Licencias.Include(l => l.Equipos).FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken);
        if (licencia is null)
        {
            return ErroresLicencia.NoEncontrada;
        }

        db.Licencias.Remove(licencia);
        auditoria.Registrar(ModuloAuditoria.Licencias, $"Eliminó la licencia {licencia.Producto}", licencia.SedeId);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal sealed class ArchivarLicenciaHandler(IAppDbContext db, IAuditoria auditoria) : ICommandHandler<ArchivarLicenciaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(ArchivarLicenciaCommand command, CancellationToken cancellationToken)
    {
        var licencia = await db.Licencias.Include(l => l.Equipos).FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken);
        if (licencia is null)
        {
            return ErroresLicencia.NoEncontrada;
        }

        if (!licencia.Archivada)
        {
            var equipos = licencia.Equipos.Count;
            licencia.Archivar();
            auditoria.Registrar(
                ModuloAuditoria.Licencias,
                equipos > 0 ? $"Archivó la licencia {licencia.Producto} y la quitó de {equipos} equipos" : $"Archivó la licencia {licencia.Producto}",
                licencia.SedeId);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Vacio.Valor;
    }
}

internal sealed class ReactivarLicenciaHandler(IAppDbContext db, IAuditoria auditoria) : ICommandHandler<ReactivarLicenciaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(ReactivarLicenciaCommand command, CancellationToken cancellationToken)
    {
        var licencia = await db.Licencias.FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken);
        if (licencia is null)
        {
            return ErroresLicencia.NoEncontrada;
        }

        if (licencia.Archivada)
        {
            licencia.Reactivar();
            auditoria.Registrar(ModuloAuditoria.Licencias, $"Reactivó la licencia {licencia.Producto}", licencia.SedeId);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Vacio.Valor;
    }
}

internal sealed class AsignarLicenciaHandler(IAppDbContext db, TimeProvider reloj, IAuditoria auditoria) : ICommandHandler<AsignarLicenciaCommand, Vacio>
{
    public const int MaximoPorVez = 500;

    public async Task<Result<Vacio>> Handle(AsignarLicenciaCommand command, CancellationToken cancellationToken)
    {
        var ids = (command.ActivoIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0 || ids.Count > MaximoPorVez)
        {
            return ErroresLicencia.SinEquipos;
        }

        var licencia = await db.Licencias.Include(l => l.Equipos).FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken);
        if (licencia is null)
        {
            return ErroresLicencia.NoEncontrada;
        }

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (licencia.Archivada)
        {
            return ErroresLicencia.Archivada;
        }

        if (licencia.FechaFin < hoy)
        {
            return ErroresLicencia.Vencida;
        }

        // El filtro de sede hace que un activo de otra red "no exista"; el superadministrador los ve todos.
        var activos = await (
            from a in db.Activos.AsNoTracking()
            join e in db.EstadosActivo on a.EstadoActivoId equals e.Id
            where ids.Contains(a.Id)
            select new { a.Id, a.SedeId, a.CodigoPatrimonial, Grupo = e.GrupoAsignado ?? GrupoEstado.EnUso })
            .ToListAsync(cancellationToken);

        if (activos.Count != ids.Count || activos.Exists(a => a.SedeId != licencia.SedeId))
        {
            return ErroresLicencia.EquipoNoEncontrado;
        }

        if (activos.Exists(a => a.Grupo == GrupoEstado.DeBaja))
        {
            return ErroresLicencia.EquipoDeBaja;
        }

        if (licencia.Equipos.Any(e => ids.Contains(e.ActivoId)))
        {
            return ErroresLicencia.YaAsignada;
        }

        if (ids.Count > licencia.PuestosLibres)
        {
            return ErroresLicencia.SinPuestos(licencia.PuestosLibres);
        }

        foreach (var id in ids)
        {
            licencia.Asignar(id, hoy);
        }

        // La clave (licencia, activo) impide repetir el equipo aunque dos personas lo asignen a la vez.
        foreach (var a in activos)
        {
            auditoria.Registrar(ModuloAuditoria.Licencias, $"Asignó la licencia {licencia.Producto} a {a.CodigoPatrimonial}", licencia.SedeId, a.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal sealed class QuitarLicenciaHandler(IAppDbContext db, IAuditoria auditoria) : ICommandHandler<QuitarLicenciaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(QuitarLicenciaCommand command, CancellationToken cancellationToken)
    {
        var licencia = await db.Licencias.Include(l => l.Equipos).FirstOrDefaultAsync(l => l.Id == command.Id, cancellationToken);
        if (licencia is null)
        {
            return ErroresLicencia.NoEncontrada;
        }

        if (!licencia.Equipos.Any(e => e.ActivoId == command.ActivoId))
        {
            return ErroresLicencia.NoAsignada;
        }

        licencia.Quitar(command.ActivoId);
        auditoria.Registrar(
            ModuloAuditoria.Licencias,
            $"Quitó la licencia {licencia.Producto} de {await db.CodigoActivoAsync(command.ActivoId, cancellationToken)}",
            licencia.SedeId,
            command.ActivoId);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}
