using Inventario.Application.Auditoria;
using Inventario.Application.Common.Archivos;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos.Gestionar;

/// <summary>Registra un activo con su ficha técnica, discos y RAM.</summary>
/// <remarks>Requiere Activos.Crear. La sede es la del establecimiento (un usuario de sede solo ve los de su red).</remarks>
public sealed record CrearActivoCommand(FichaActivo Ficha) : ICommand<IdCreado>;

/// <summary>Reemplaza la ficha de un activo, incluidos discos y RAM. No cambia de sede.</summary>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record EditarActivoCommand(Guid Id, FichaActivo Ficha) : ICommand<Vacio>;

/// <summary>Elimina un activo registrado por error, con sus discos y RAM.</summary>
/// <remarks>Requiere Activos.Eliminar. Para un equipo que ya no se usa, lo habitual es cambiar su estado a "De baja".</remarks>
public sealed record EliminarActivoCommand(Guid Id) : ICommand<Vacio>;

internal sealed class CrearActivoHandler(IAppDbContext db, VerificadorFichaActivo verificador, IAuditoria auditoria)
    : ICommandHandler<CrearActivoCommand, IdCreado>
{
    public async Task<Result<IdCreado>> Handle(CrearActivoCommand command, CancellationToken cancellationToken)
    {
        var ficha = command.Ficha;
        var verificado = await verificador.VerificarAsync(ficha, actual: null, cancellationToken);
        if (!verificado.EsExito)
        {
            return verificado.Error!;
        }

        var activo = Activo.Crear(
            verificado.Valor,
            ficha.EstablecimientoId,
            ficha.Generales,
            ficha.Adquisicion ?? DatosAdquisicionActivo.Vacios,
            ficha.Tecnicos ?? DatosTecnicosActivo.Vacios,
            ficha.Discos ?? [],
            ficha.ModulosRam ?? []);

        db.Activos.Add(activo);
        auditoria.Registrar(ModuloAuditoria.Activos, $"Registró el activo {activo.CodigoPatrimonial}", activo.SedeId, activo.Id);
        await db.SaveChangesAsync(cancellationToken);

        return new IdCreado(activo.Id);
    }
}

internal sealed class EditarActivoHandler(IAppDbContext db, VerificadorFichaActivo verificador, IAuditoria auditoria)
    : ICommandHandler<EditarActivoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EditarActivoCommand command, CancellationToken cancellationToken)
    {
        // El filtro de sede hace que un activo de otra sede "no exista" (404).
        var activo = await db.Activos
            .Include(a => a.Discos)
            .Include(a => a.ModulosRam)
            .FirstOrDefaultAsync(a => a.Id == command.Id, cancellationToken);

        if (activo is null)
        {
            return ErroresActivo.NoEncontrado;
        }

        var ficha = command.Ficha;
        var verificado = await verificador.VerificarAsync(ficha, activo, cancellationToken);
        if (!verificado.EsExito)
        {
            return verificado.Error!;
        }

        activo.Actualizar(
            ficha.EstablecimientoId,
            ficha.Generales,
            ficha.Adquisicion ?? DatosAdquisicionActivo.Vacios,
            ficha.Tecnicos ?? DatosTecnicosActivo.Vacios,
            ficha.Discos ?? [],
            ficha.ModulosRam ?? []);

        auditoria.Registrar(ModuloAuditoria.Activos, $"Editó el activo {activo.CodigoPatrimonial}", activo.SedeId, activo.Id);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal sealed class EliminarActivoHandler(IAppDbContext db, IAlmacenArchivos almacen, IAuditoria auditoria) : ICommandHandler<EliminarActivoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EliminarActivoCommand command, CancellationToken cancellationToken)
    {
        var activo = await db.Activos
            .Include(a => a.Discos)
            .Include(a => a.ModulosRam)
            .FirstOrDefaultAsync(a => a.Id == command.Id, cancellationToken);

        if (activo is null)
        {
            return ErroresActivo.NoEncontrado;
        }

        // Las fotos se borran con el activo (en cascada); sus archivos, después de guardar.
        var fotos = await db.FotosActivo.Where(f => f.ActivoId == activo.Id).ToListAsync(cancellationToken);

        db.Activos.Remove(activo);
        auditoria.Registrar(ModuloAuditoria.Activos, $"Eliminó el activo {activo.CodigoPatrimonial} ({activo.Modelo})", activo.SedeId, activo.Id);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var foto in fotos)
        {
            await almacen.EliminarAsync(foto.Clave, cancellationToken);
            await almacen.EliminarAsync(foto.ClaveMiniatura, cancellationToken);
        }

        return Vacio.Valor;
    }
}
