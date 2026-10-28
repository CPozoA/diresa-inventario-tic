using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Asignaciones;

/// <summary>Una asignación de un activo, con los datos de la persona (ficha del activo, D51).</summary>
/// <param name="Id">Asignación.</param>
/// <param name="PersonaId">Persona.</param>
/// <param name="Dni">DNI.</param>
/// <param name="NombreCompleto">Nombre completo, en mayúsculas.</param>
/// <param name="Cargo">Cargo.</param>
/// <param name="Establecimiento">Establecimiento donde trabaja.</param>
/// <param name="Area">Área donde trabaja.</param>
/// <param name="Verificada">Falso si sus nombres se escribieron a mano ("Sin verificar en RENIEC").</param>
/// <param name="FechaEntrega">Fecha de entrega.</param>
/// <param name="ConservacionEntrega">Cómo estaba el equipo al entregarlo.</param>
/// <param name="FechaDevolucion">Fecha de devolución; nula mientras la tiene.</param>
/// <param name="ConservacionDevolucion">Cómo estaba al devolverlo.</param>
/// <param name="Observaciones">Notas de la devolución.</param>
public sealed record AsignacionActivoDto(
    Guid Id,
    Guid PersonaId,
    string Dni,
    string NombreCompleto,
    string? Cargo,
    string Establecimiento,
    string Area,
    bool Verificada,
    DateOnly FechaEntrega,
    Conservacion ConservacionEntrega,
    DateOnly? FechaDevolucion,
    Conservacion? ConservacionDevolucion,
    string? Observaciones);

/// <summary>Qué pasó con el equipo.</summary>
public enum TipoMovimiento
{
    Entrega,
    Devolucion,
}

/// <summary>Una entrega o una devolución, para la lista de movimientos de la red.</summary>
/// <param name="AsignacionId">Asignación.</param>
/// <param name="Tipo">Entrega o devolución.</param>
/// <param name="Fecha">Fecha del movimiento.</param>
/// <param name="ActivoId">Activo.</param>
/// <param name="CodigoPatrimonial">Código patrimonial.</param>
/// <param name="TipoActivo">Tipo de activo.</param>
/// <param name="Marca">Marca.</param>
/// <param name="Modelo">Modelo.</param>
/// <param name="PersonaId">Persona.</param>
/// <param name="NombreCompleto">Nombre de la persona.</param>
/// <param name="Conservacion">Conservación registrada en ese movimiento.</param>
/// <param name="SedeNombre">Red (para la vista consolidada del superadministrador).</param>
public sealed record MovimientoDto(
    Guid AsignacionId,
    TipoMovimiento Tipo,
    DateOnly Fecha,
    Guid ActivoId,
    string CodigoPatrimonial,
    string TipoActivo,
    string Marca,
    string Modelo,
    Guid PersonaId,
    string NombreCompleto,
    Conservacion Conservacion,
    string SedeNombre);

/// <summary>Errores esperados de las asignaciones.</summary>
public static class ErroresAsignacion
{
    public static readonly ErrorAplicacion PersonaInvalida =
        ErrorAplicacion.Validacion("Asignaciones.PersonaInvalida", "La persona no existe o no pertenece a su red.");

    public static readonly ErrorAplicacion OtraRed =
        ErrorAplicacion.ReglaDeNegocio("Asignaciones.OtraRed", "La persona es de otra red. Solo se asignan equipos al personal de la misma red.");

    public static readonly ErrorAplicacion PersonaCesada =
        ErrorAplicacion.ReglaDeNegocio("Asignaciones.PersonaCesada", "La persona está marcada como cesada. Reactívela para asignarle equipos.");

    public static readonly ErrorAplicacion ActivoDeBaja =
        ErrorAplicacion.ReglaDeNegocio("Asignaciones.ActivoDeBaja", "El activo está de baja: no se puede asignar.");

    public static readonly ErrorAplicacion YaLaTiene =
        ErrorAplicacion.Conflicto("Asignaciones.YaLaTiene", "El activo ya está asignado a esa persona.");

    public static readonly ErrorAplicacion SinAsignacion =
        ErrorAplicacion.ReglaDeNegocio("Asignaciones.SinAsignacion", "El activo no está asignado a nadie: no hay nada que devolver.");

    public static readonly ErrorAplicacion FechaFutura =
        ErrorAplicacion.Validacion("Asignaciones.FechaFutura", "La fecha no puede ser posterior a hoy.");

    public static readonly ErrorAplicacion EstadoInvalido =
        ErrorAplicacion.Validacion("Asignaciones.EstadoInvalido", "El estado elegido no existe o está desactivado.");

    public static ErrorAplicacion FechaAnterior(DateOnly desde) =>
        ErrorAplicacion.Validacion(
            "Asignaciones.FechaAnterior",
            $"La fecha no puede ser anterior al {desde:dd/MM/yyyy}, el último movimiento del equipo.");
}

internal static class ConsultasAsignacion
{
    /// <summary>Asignaciones del activo con los datos de la persona: la vigente primero y luego las más recientes.</summary>
    public static IQueryable<AsignacionActivoDto> DelActivo(IAppDbContext db, Guid activoId, bool soloVigente = false)
    {
        var asignaciones = db.Asignaciones.AsNoTracking().Where(x => x.ActivoId == activoId);
        if (soloVigente)
        {
            asignaciones = asignaciones.Where(x => x.FechaDevolucion == null);
        }

        return
            from x in asignaciones
            join p in db.Personal on x.PersonaId equals p.Id
            join e in db.Establecimientos on p.EstablecimientoId equals e.Id
            join ar in db.Areas on p.AreaId equals ar.Id
            orderby x.FechaDevolucion == null descending, x.FechaEntrega descending, x.CreadoEn descending
            select new AsignacionActivoDto(
                x.Id,
                p.Id,
                p.Dni,
                p.NombreCompleto,
                p.Cargo,
                e.Nombre,
                ar.Nombre,
                p.VerificadaEn != null,
                x.FechaEntrega,
                x.ConservacionEntrega,
                x.FechaDevolucion,
                x.ConservacionDevolucion,
                x.Observaciones);
    }

    /// <summary>La asignación vigente del activo, o nula.</summary>
    public static Task<AsignacionActivoDto?> VigenteAsync(IAppDbContext db, Guid activoId, CancellationToken cancellationToken) =>
        DelActivo(db, activoId, soloVigente: true).FirstOrDefaultAsync(cancellationToken);

    /// <summary>Último movimiento del activo: la fecha más reciente de entrega o devolución.</summary>
    public static async Task<DateOnly?> UltimoMovimientoAsync(IAppDbContext db, Guid activoId, CancellationToken cancellationToken)
    {
        var fechas = await db.Asignaciones.AsNoTracking()
            .Where(x => x.ActivoId == activoId)
            .Select(x => new { x.FechaEntrega, x.FechaDevolucion })
            .ToListAsync(cancellationToken);

        return fechas.Count == 0
            ? null
            : fechas.Max(f => f.FechaDevolucion is DateOnly devolucion && devolucion > f.FechaEntrega ? devolucion : f.FechaEntrega);
    }
}
