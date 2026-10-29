using Inventario.Domain.Common;

namespace Inventario.Domain.Mantenimientos;

/// <summary>Preventivo (programado: limpieza, revisión) o correctivo (cuando algo falla).</summary>
public enum TipoMantenimiento
{
    Preventivo,
    Correctivo,
}

/// <summary>Quién hace el mantenimiento.</summary>
public enum EjecutorMantenimiento
{
    /// <summary>El área de TI de la red: se indica el técnico (un usuario del sistema).</summary>
    PersonalTi,

    /// <summary>Una empresa externa: nombre, orden de servicio y costo.</summary>
    Proveedor,
}

/// <summary>Cómo quedó el equipo.</summary>
public enum ResultadoMantenimiento
{
    Operativo,
    SinReparacion,
}

/// <summary>Quién hace el mantenimiento.</summary>
/// <param name="Ejecutor">Personal de TI o proveedor.</param>
/// <param name="TecnicoId">Usuario técnico (solo personal de TI).</param>
/// <param name="Proveedor">Empresa (solo proveedor).</param>
/// <param name="OrdenServicio">N° de orden de servicio (solo proveedor, opcional).</param>
/// <param name="Costo">Costo en soles (solo proveedor, opcional).</param>
public sealed record DatosEjecutor(
    EjecutorMantenimiento Ejecutor,
    Guid? TecnicoId,
    string? Proveedor,
    string? OrdenServicio,
    decimal? Costo);

/// <summary>Cierre de un mantenimiento.</summary>
/// <param name="FechaFin">Fecha de término.</param>
/// <param name="TrabajoRealizado">Qué se hizo.</param>
/// <param name="Resultado">Cómo quedó el equipo.</param>
/// <param name="Costo">Costo final en soles (solo proveedor; nulo conserva el que tenía).</param>
public sealed record DatosCierre(DateOnly FechaFin, string TrabajoRealizado, ResultadoMantenimiento Resultado, decimal? Costo);

/// <summary>
/// Mantenimiento de un activo (docs/ARQUITECTURA.md D53, §5.13). Está abierto mientras no tenga fecha de término; un
/// activo tiene a lo sumo uno abierto. No se elimina: es el historial del equipo.
/// </summary>
public sealed class Mantenimiento : EntidadAuditable, IPerteneceASede
{
    public const int LongitudMaximaDetalle = 1000;
    public const int LongitudMaximaProveedor = 150;
    public const int LongitudMaximaOrdenServicio = 50;
    public const decimal CostoMaximo = 9_999_999.99m;

    // Requerido por EF Core.
    private Mantenimiento()
    {
    }

    private Mantenimiento(Guid sedeId, Guid activoId)
    {
        if (sedeId == Guid.Empty || activoId == Guid.Empty)
        {
            throw new DominioException("El mantenimiento debe indicar el activo.");
        }

        SedeId = sedeId;
        ActivoId = activoId;
    }

    public Guid SedeId { get; private set; }

    public Guid ActivoId { get; private set; }

    public TipoMantenimiento Tipo { get; private set; }

    public DateOnly FechaInicio { get; private set; }

    /// <summary>Correctivo: el problema reportado (obligatorio). Preventivo: las actividades previstas (opcional).</summary>
    public string? Detalle { get; private set; }

    public EjecutorMantenimiento Ejecutor { get; private set; }

    public Guid? TecnicoId { get; private set; }

    public string? Proveedor { get; private set; }

    public string? OrdenServicio { get; private set; }

    public decimal? Costo { get; private set; }

    public DateOnly? FechaFin { get; private set; }

    public string? TrabajoRealizado { get; private set; }

    public ResultadoMantenimiento? Resultado { get; private set; }

    public bool Abierto => !FechaFin.HasValue;

    /// <summary>Abre un mantenimiento. <paramref name="hoy"/> es la fecha actual: el inicio no puede ser futuro.</summary>
    public static Mantenimiento Abrir(
        Guid sedeId, Guid activoId, TipoMantenimiento tipo, DateOnly fechaInicio, string? detalle, DatosEjecutor ejecutor, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(ejecutor);

        if (!Enum.IsDefined(tipo))
        {
            throw new DominioException("Elija el tipo de mantenimiento.");
        }

        if (fechaInicio > hoy)
        {
            throw new DominioException("La fecha de inicio no puede ser posterior a hoy.");
        }

        var texto = Guardas.TextoOpcional(detalle, tipo == TipoMantenimiento.Correctivo ? "El problema reportado" : "Las actividades previstas", LongitudMaximaDetalle);
        if (tipo == TipoMantenimiento.Correctivo && texto is null)
        {
            throw new DominioException("Describa el problema reportado.");
        }

        var m = new Mantenimiento(sedeId, activoId)
        {
            Tipo = tipo,
            FechaInicio = fechaInicio,
            Detalle = texto,
        };
        m.AsignarEjecutor(ejecutor);
        return m;
    }

    /// <summary>Registra el término. Desde entonces es historial.</summary>
    public void Cerrar(DatosCierre cierre, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(cierre);

        if (!Abierto)
        {
            throw new DominioException("Este mantenimiento ya se cerró.");
        }

        if (cierre.FechaFin < FechaInicio)
        {
            throw new DominioException("La fecha de término no puede ser anterior a la de inicio.");
        }

        if (cierre.FechaFin > hoy)
        {
            throw new DominioException("La fecha de término no puede ser posterior a hoy.");
        }

        if (!Enum.IsDefined(cierre.Resultado))
        {
            throw new DominioException("Indique cómo quedó el equipo.");
        }

        var trabajo = Guardas.TextoObligatorio(cierre.TrabajoRealizado, "El trabajo realizado", LongitudMaximaDetalle);

        if (cierre.Costo is decimal costo)
        {
            if (Ejecutor != EjecutorMantenimiento.Proveedor)
            {
                throw new DominioException("El costo solo se registra cuando lo hizo un proveedor.");
            }

            Costo = ValidarCosto(costo);
        }

        FechaFin = cierre.FechaFin;
        TrabajoRealizado = trabajo;
        Resultado = cierre.Resultado;
    }

    private void AsignarEjecutor(DatosEjecutor e)
    {
        switch (e.Ejecutor)
        {
            case EjecutorMantenimiento.PersonalTi:
                if (e.TecnicoId is not Guid tecnico || tecnico == Guid.Empty)
                {
                    throw new DominioException("Elija al técnico.");
                }

                Ejecutor = EjecutorMantenimiento.PersonalTi;
                TecnicoId = tecnico;
                Proveedor = null;
                OrdenServicio = null;
                Costo = null;
                break;

            case EjecutorMantenimiento.Proveedor:
                Ejecutor = EjecutorMantenimiento.Proveedor;
                TecnicoId = null;
                Proveedor = Guardas.TextoObligatorio(e.Proveedor, "La empresa", LongitudMaximaProveedor);
                OrdenServicio = Guardas.TextoOpcional(e.OrdenServicio, "El N° de orden de servicio", LongitudMaximaOrdenServicio)?.ToUpperInvariant();
                Costo = e.Costo is decimal c ? ValidarCosto(c) : null;
                break;

            default:
                throw new DominioException("Indique quién hace el mantenimiento.");
        }
    }

    private static decimal ValidarCosto(decimal costo)
    {
        if (costo < 0 || costo > CostoMaximo || decimal.Round(costo, 2) != costo)
        {
            throw new DominioException("El costo debe ser un monto positivo con hasta 2 decimales.");
        }

        return costo;
    }
}
