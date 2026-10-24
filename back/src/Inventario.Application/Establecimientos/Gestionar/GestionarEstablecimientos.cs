using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Establecimientos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Establecimientos.Gestionar;

/// <summary>Registra un establecimiento en una red. Id_Establecimiento y código único no se repiten en todo el sistema.</summary>
/// <remarks>Requiere Establecimientos.Gestionar (solo el superadministrador).</remarks>
public sealed record CrearEstablecimientoCommand(
    Guid SedeId,
    int IdEstablecimiento,
    int CodigoUnico,
    string Nombre,
    string? Categoria,
    string? Microred,
    string? Provincia,
    string? Distrito,
    string? Ubigeo) : ICommand<IdCreado>;

/// <summary>Corrige los datos de un establecimiento. La red (sede) no cambia.</summary>
/// <remarks>Requiere Establecimientos.Gestionar (solo el superadministrador).</remarks>
public sealed record EditarEstablecimientoCommand(Guid Id, DatosEstablecimiento Datos) : ICommand<Vacio>;

/// <summary>Activa o desactiva un establecimiento. Desactivado, deja de ofrecerse al registrar activos.</summary>
/// <remarks>Requiere Establecimientos.Gestionar (solo el superadministrador).</remarks>
public sealed record CambiarEstadoEstablecimientoCommand(Guid Id, bool Activo) : ICommand<Vacio>;

internal sealed class CrearEstablecimientoHandler(IAppDbContext db) : ICommandHandler<CrearEstablecimientoCommand, IdCreado>
{
    public async Task<Result<IdCreado>> Handle(CrearEstablecimientoCommand command, CancellationToken cancellationToken)
    {
        if (!await db.Sedes.AnyAsync(s => s.Id == command.SedeId, cancellationToken))
        {
            return ErroresEstablecimiento.SedeNoEncontrada;
        }

        // El dominio valida los datos (obligatorios, longitudes, ubigeo de 6 dígitos).
        var establecimiento = Establecimiento.Crear(
            command.SedeId,
            new DatosEstablecimiento(
                command.IdEstablecimiento,
                command.CodigoUnico,
                command.Nombre,
                command.Categoria,
                command.Microred,
                command.Provincia,
                command.Distrito,
                command.Ubigeo));

        var duplicado = await UnicidadEstablecimiento.VerificarAsync(db, establecimiento, cancellationToken);
        if (duplicado is not null)
        {
            return duplicado;
        }

        db.Establecimientos.Add(establecimiento);
        await db.SaveChangesAsync(cancellationToken);

        return new IdCreado(establecimiento.Id);
    }
}

internal sealed class EditarEstablecimientoHandler(IAppDbContext db) : ICommandHandler<EditarEstablecimientoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EditarEstablecimientoCommand command, CancellationToken cancellationToken)
    {
        var establecimiento = await db.Establecimientos.FirstOrDefaultAsync(e => e.Id == command.Id, cancellationToken);
        if (establecimiento is null)
        {
            return ErroresEstablecimiento.NoEncontrado;
        }

        establecimiento.Actualizar(command.Datos);

        var duplicado = await UnicidadEstablecimiento.VerificarAsync(db, establecimiento, cancellationToken);
        if (duplicado is not null)
        {
            return duplicado;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal sealed class CambiarEstadoEstablecimientoHandler(IAppDbContext db)
    : ICommandHandler<CambiarEstadoEstablecimientoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CambiarEstadoEstablecimientoCommand command, CancellationToken cancellationToken)
    {
        var establecimiento = await db.Establecimientos.FirstOrDefaultAsync(e => e.Id == command.Id, cancellationToken);
        if (establecimiento is null)
        {
            return ErroresEstablecimiento.NoEncontrado;
        }

        if (command.Activo)
        {
            establecimiento.Activar();
        }
        else
        {
            establecimiento.Desactivar();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal static class UnicidadEstablecimiento
{
    /// <summary>Error si otro establecimiento ya usa el mismo Id_Establecimiento o código único; si no, <c>null</c>.</summary>
    public static async Task<ErrorAplicacion?> VerificarAsync(
        IAppDbContext db,
        Establecimiento establecimiento,
        CancellationToken cancellationToken)
    {
        // Lo usa solo el superadministrador, que ve todas las sedes: la comprobación abarca todo el sistema.
        var otros = db.Establecimientos.Where(e => e.Id != establecimiento.Id);

        if (await otros.AnyAsync(e => e.IdEstablecimiento == establecimiento.IdEstablecimiento, cancellationToken))
        {
            return ErroresEstablecimiento.IdDuplicado;
        }

        if (await otros.AnyAsync(e => e.CodigoUnico == establecimiento.CodigoUnico, cancellationToken))
        {
            return ErroresEstablecimiento.CodigoUnicoDuplicado;
        }

        return null;
    }
}
