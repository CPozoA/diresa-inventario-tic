using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Reniec;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Personal;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Personal;

/// <summary>
/// Registra a una persona en la red del establecimiento (D50). El servidor vuelve a consultar el DNI: si RENIEC lo
/// encuentra, se usan sus nombres (los escritos se ignoran) y la persona queda verificada; si no lo encuentra o no
/// responde, se usan los nombres escritos y queda "sin verificar".
/// </summary>
/// <param name="Dni">8 dígitos; no se repite en la red.</param>
/// <param name="EstablecimientoId">Establecimiento donde trabaja (define la red).</param>
/// <param name="AreaId">Área de la misma red.</param>
/// <param name="Cargo">Opcional.</param>
/// <param name="ApellidoPaterno">Solo si RENIEC no da los datos.</param>
/// <param name="ApellidoMaterno">Solo si RENIEC no da los datos.</param>
/// <param name="Nombres">Solo si RENIEC no da los datos.</param>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record RegistrarPersonaCommand(
    string? Dni,
    Guid EstablecimientoId,
    Guid AreaId,
    string? Cargo,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? Nombres) : ICommand<IdCreado>;

/// <summary>
/// Cambia el establecimiento, el área y el cargo. Los nombres solo se corrigen si la persona no está verificada
/// (los de RENIEC no se cambian a mano; si se envían, se ignoran). El DNI no cambia.
/// </summary>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record EditarPersonaCommand(
    Guid Id,
    Guid EstablecimientoId,
    Guid AreaId,
    string? Cargo,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? Nombres) : ICommand<Vacio>;

/// <summary>Consulta RENIEC otra vez y, si encuentra el DNI, reemplaza los nombres escritos a mano y la marca como verificada.</summary>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record VerificarPersonaCommand(Guid Id) : ICommand<Vacio>;

/// <summary>Marca a la persona como cesada: ya no se le asignan equipos. Antes debe devolver los que tiene.</summary>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record CesarPersonaCommand(Guid Id) : ICommand<Vacio>;

/// <summary>Vuelve a marcar a la persona como activa.</summary>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record ReactivarPersonaCommand(Guid Id) : ICommand<Vacio>;

internal sealed class RegistrarPersonaHandler(IAppDbContext db, IConsultaDni reniec, TimeProvider reloj)
    : ICommandHandler<RegistrarPersonaCommand, IdCreado>
{
    public async Task<Result<IdCreado>> Handle(RegistrarPersonaCommand command, CancellationToken cancellationToken)
    {
        if (TextoDni.Limpiar(command.Dni) is not string dni)
        {
            return ErroresPersonal.DniInvalido;
        }

        var lugar = await LugarDeTrabajo.VerificarAsync(db, command.EstablecimientoId, command.AreaId, actual: null, cancellationToken);
        if (!lugar.EsExito)
        {
            return lugar.Error!;
        }

        var sedeId = lugar.Valor;
        if (await db.Personal.AnyAsync(p => p.SedeId == sedeId && p.Dni == dni, cancellationToken))
        {
            return ErroresPersonal.DniDuplicado;
        }

        var laborales = new DatosLaboralesPersona(command.EstablecimientoId, command.AreaId, command.Cargo);
        var consulta = await reniec.ConsultarAsync(dni, cancellationToken);

        Persona persona;
        if (consulta is { Estado: EstadoConsultaDni.Encontrado, Nombres: NombresPersona deReniec })
        {
            persona = Persona.RegistrarVerificada(sedeId, dni, deReniec, laborales, reloj.GetUtcNow());
        }
        else
        {
            if (string.IsNullOrWhiteSpace(command.ApellidoPaterno) || string.IsNullOrWhiteSpace(command.Nombres))
            {
                return ErroresPersonal.NombresObligatorios(consulta.Estado);
            }

            persona = Persona.RegistrarSinVerificar(
                sedeId, dni, new NombresPersona(command.ApellidoPaterno, command.ApellidoMaterno, command.Nombres), laborales);
        }

        db.Personal.Add(persona);
        await db.SaveChangesAsync(cancellationToken);
        return new IdCreado(persona.Id);
    }
}

internal sealed class EditarPersonaHandler(IAppDbContext db) : ICommandHandler<EditarPersonaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EditarPersonaCommand command, CancellationToken cancellationToken)
    {
        var persona = await db.Personal.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (persona is null)
        {
            return ErroresPersonal.NoEncontrada;
        }

        var lugar = await LugarDeTrabajo.VerificarAsync(db, command.EstablecimientoId, command.AreaId, persona, cancellationToken);
        if (!lugar.EsExito)
        {
            return lugar.Error!;
        }

        persona.CambiarDatosLaborales(new DatosLaboralesPersona(command.EstablecimientoId, command.AreaId, command.Cargo));

        var cambiaNombres = !string.IsNullOrWhiteSpace(command.ApellidoPaterno) || !string.IsNullOrWhiteSpace(command.Nombres);
        if (!persona.Verificada && cambiaNombres)
        {
            persona.CorregirNombres(new NombresPersona(command.ApellidoPaterno ?? string.Empty, command.ApellidoMaterno, command.Nombres ?? string.Empty));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal sealed class VerificarPersonaHandler(IAppDbContext db, IConsultaDni reniec, TimeProvider reloj)
    : ICommandHandler<VerificarPersonaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(VerificarPersonaCommand command, CancellationToken cancellationToken)
    {
        var persona = await db.Personal.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (persona is null)
        {
            return ErroresPersonal.NoEncontrada;
        }

        var consulta = await reniec.ConsultarAsync(persona.Dni, cancellationToken);
        switch (consulta)
        {
            case { Estado: EstadoConsultaDni.Encontrado, Nombres: NombresPersona deReniec }:
                persona.Verificar(deReniec, reloj.GetUtcNow());
                await db.SaveChangesAsync(cancellationToken);
                return Vacio.Valor;
            case { Estado: EstadoConsultaDni.NoEncontrado }:
                return ErroresPersonal.DniNoEncontrado;
            default:
                return ErroresPersonal.ReniecNoDisponible;
        }
    }
}

internal sealed class CesarPersonaHandler(IAppDbContext db) : ICommandHandler<CesarPersonaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CesarPersonaCommand command, CancellationToken cancellationToken)
    {
        var persona = await db.Personal.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (persona is null)
        {
            return ErroresPersonal.NoEncontrada;
        }

        var equipos = await db.Asignaciones.CountAsync(x => x.PersonaId == persona.Id && x.FechaDevolucion == null, cancellationToken);
        if (equipos > 0)
        {
            return ErroresPersonal.TieneEquipos(equipos);
        }

        persona.Cesar();
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal sealed class ReactivarPersonaHandler(IAppDbContext db) : ICommandHandler<ReactivarPersonaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(ReactivarPersonaCommand command, CancellationToken cancellationToken)
    {
        var persona = await db.Personal.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (persona is null)
        {
            return ErroresPersonal.NoEncontrada;
        }

        persona.Reactivar();
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

/// <summary>Comprueba que el establecimiento y el área existan, estén activos y sean de la misma red.</summary>
internal static class LugarDeTrabajo
{
    /// <returns>La red, o el error.</returns>
    public static async Task<Result<Guid>> VerificarAsync(
        IAppDbContext db, Guid establecimientoId, Guid areaId, Persona? actual, CancellationToken cancellationToken)
    {
        // El filtro de sede oculta los establecimientos de otras redes a un usuario de sede.
        var establecimiento = await db.Establecimientos.AsNoTracking()
            .Where(e => e.Id == establecimientoId)
            .Select(e => new { e.SedeId, e.Activo })
            .FirstOrDefaultAsync(cancellationToken);

        // Un establecimiento desactivado solo se acepta si la persona ya estaba en él.
        if (establecimiento is null || (!establecimiento.Activo && actual?.EstablecimientoId != establecimientoId))
        {
            return ErroresPersonal.EstablecimientoInvalido;
        }

        if (actual is not null && establecimiento.SedeId != actual.SedeId)
        {
            return ErroresPersonal.OtraRed;
        }

        // Un área desactivada, igual: solo si ya era la suya.
        var areaActual = actual?.AreaId;
        var areaValida = await db.Areas.AsNoTracking()
            .AnyAsync(a => a.Id == areaId && a.SedeId == establecimiento.SedeId && (a.Activo || a.Id == areaActual), cancellationToken);
        if (!areaValida)
        {
            return ErroresPersonal.AreaInvalida;
        }

        return establecimiento.SedeId;
    }
}
