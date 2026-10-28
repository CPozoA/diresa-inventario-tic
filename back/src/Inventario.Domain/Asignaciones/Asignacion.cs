using Inventario.Domain.Activos;
using Inventario.Domain.Common;

namespace Inventario.Domain.Asignaciones;

/// <summary>
/// Entrega de un activo a una persona de la misma red (docs/ARQUITECTURA.md D51, §5.12). Está vigente mientras no
/// tenga fecha de devolución; un activo tiene a lo sumo una asignación vigente. Cada entrega y cada devolución
/// registra la fecha y el estado de conservación del equipo en ese momento. Las asignaciones no se eliminan: son el
/// historial del equipo y de la persona.
/// </summary>
public sealed class Asignacion : EntidadAuditable, IPerteneceASede
{
    public const int LongitudMaximaObservaciones = 500;

    // Requerido por EF Core.
    private Asignacion()
    {
    }

    private Asignacion(Guid sedeId, Guid activoId, Guid personaId, DateOnly fechaEntrega, Conservacion conservacion)
    {
        if (sedeId == Guid.Empty || activoId == Guid.Empty || personaId == Guid.Empty)
        {
            throw new DominioException("La asignación debe indicar el activo y la persona.");
        }

        ValidarConservacion(conservacion, "Elija el estado de conservación al entregar.");

        SedeId = sedeId;
        ActivoId = activoId;
        PersonaId = personaId;
        FechaEntrega = fechaEntrega;
        ConservacionEntrega = conservacion;
    }

    public Guid SedeId { get; private set; }

    public Guid ActivoId { get; private set; }

    public Guid PersonaId { get; private set; }

    public DateOnly FechaEntrega { get; private set; }

    public Conservacion ConservacionEntrega { get; private set; }

    public DateOnly? FechaDevolucion { get; private set; }

    public Conservacion? ConservacionDevolucion { get; private set; }

    /// <summary>Notas de la devolución ("se devuelve sin cargador").</summary>
    public string? Observaciones { get; private set; }

    public bool Vigente => !FechaDevolucion.HasValue;

    /// <summary>
    /// Entrega el activo a la persona. <paramref name="hoy"/> es la fecha actual en la zona horaria del sistema:
    /// la entrega no puede ser futura.
    /// </summary>
    public static Asignacion Entregar(
        Guid sedeId, Guid activoId, Guid personaId, DateOnly fechaEntrega, Conservacion conservacion, DateOnly hoy)
    {
        if (fechaEntrega > hoy)
        {
            throw new DominioException("La fecha de entrega no puede ser posterior a hoy.");
        }

        return new Asignacion(sedeId, activoId, personaId, fechaEntrega, conservacion);
    }

    /// <summary>Registra la devolución. Desde entonces la asignación forma parte del historial.</summary>
    public void Devolver(DateOnly fecha, Conservacion conservacion, string? observaciones, DateOnly hoy)
    {
        if (!Vigente)
        {
            throw new DominioException("Esta asignación ya se devolvió.");
        }

        if (fecha < FechaEntrega)
        {
            throw new DominioException("La fecha de devolución no puede ser anterior a la de entrega.");
        }

        if (fecha > hoy)
        {
            throw new DominioException("La fecha de devolución no puede ser posterior a hoy.");
        }

        ValidarConservacion(conservacion, "Elija el estado de conservación al devolver.");

        Observaciones = Guardas.TextoOpcional(observaciones, "Las observaciones", LongitudMaximaObservaciones);
        FechaDevolucion = fecha;
        ConservacionDevolucion = conservacion;
    }

    private static void ValidarConservacion(Conservacion conservacion, string mensaje)
    {
        if (!Enum.IsDefined(conservacion))
        {
            throw new DominioException(mensaje);
        }
    }
}
