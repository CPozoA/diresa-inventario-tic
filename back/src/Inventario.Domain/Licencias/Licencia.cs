using Inventario.Domain.Common;

namespace Inventario.Domain.Licencias;

/// <summary>Datos de una suscripción de software.</summary>
/// <param name="Producto">Nombre comercial, por ejemplo "Microsoft 365 Empresa Estándar".</param>
/// <param name="Proveedor">Empresa que la vendió (opcional).</param>
/// <param name="Puestos">Cuántos equipos puede cubrir.</param>
/// <param name="FechaInicio">Desde cuándo rige.</param>
/// <param name="FechaFin">Hasta cuándo rige (último día incluido).</param>
/// <param name="DocumentoCompra">Orden de compra, contrato o factura (opcional).</param>
/// <param name="Costo">Costo total en soles (opcional).</param>
/// <param name="Observaciones">Notas (opcional).</param>
public sealed record DatosLicencia(
    string Producto,
    string? Proveedor,
    int Puestos,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? DocumentoCompra,
    decimal? Costo,
    string? Observaciones);

/// <summary>
/// Suscripción de software de una red (docs/ARQUITECTURA.md D54, §5.14): un número de puestos que se asignan a equipos
/// del inventario, con fecha de vencimiento. No se guarda la clave del producto. Archivada (no se renovó), deja de
/// contar en las alertas y libera sus equipos.
/// </summary>
public sealed class Licencia : EntidadAuditable, IPerteneceASede
{
    public const int LongitudMaximaProducto = 150;
    public const int LongitudMaximaProveedor = 150;
    public const int LongitudMaximaDocumento = 50;
    public const int LongitudMaximaObservaciones = 500;
    public const int PuestosMaximos = 100_000;
    public const decimal CostoMaximo = 99_999_999.99m;

    private readonly List<EquipoLicencia> _equipos = [];

    // Requerido por EF Core.
    private Licencia()
    {
    }

    private Licencia(Guid sedeId)
    {
        if (sedeId == Guid.Empty)
        {
            throw new DominioException("La licencia debe pertenecer a una sede.");
        }

        SedeId = sedeId;
    }

    public Guid SedeId { get; private set; }

    public string Producto { get; private set; } = string.Empty;

    public string? Proveedor { get; private set; }

    public int Puestos { get; private set; }

    public DateOnly FechaInicio { get; private set; }

    public DateOnly FechaFin { get; private set; }

    public string? DocumentoCompra { get; private set; }

    public decimal? Costo { get; private set; }

    public string? Observaciones { get; private set; }

    /// <summary>No se renovó: no cuenta en las alertas ni admite equipos.</summary>
    public bool Archivada { get; private set; }

    /// <summary>Equipos que ocupan un puesto.</summary>
    public IReadOnlyCollection<EquipoLicencia> Equipos => _equipos;

    public int PuestosLibres => Math.Max(0, Puestos - _equipos.Count);

    public static Licencia Registrar(Guid sedeId, DatosLicencia datos)
    {
        var licencia = new Licencia(sedeId);
        licencia.Actualizar(datos);
        return licencia;
    }

    /// <summary>
    /// Cambia los datos (también para renovarla: nuevas fechas). Se pueden dejar menos puestos que equipos asignados:
    /// la licencia queda "excedida" hasta que se quiten equipos.
    /// </summary>
    public void Actualizar(DatosLicencia datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (datos.Puestos < 1 || datos.Puestos > PuestosMaximos)
        {
            throw new DominioException("Indique cuántos puestos tiene la licencia (al menos 1).");
        }

        if (datos.FechaFin <= datos.FechaInicio)
        {
            throw new DominioException("La fecha de vencimiento debe ser posterior a la de inicio.");
        }

        if (datos.Costo is decimal costo && (costo < 0 || costo > CostoMaximo || decimal.Round(costo, 2) != costo))
        {
            throw new DominioException("El costo debe ser un monto positivo con hasta 2 decimales.");
        }

        Producto = Guardas.TextoObligatorio(datos.Producto, "El producto", LongitudMaximaProducto);
        Proveedor = Guardas.TextoOpcional(datos.Proveedor, "El proveedor", LongitudMaximaProveedor);
        Puestos = datos.Puestos;
        FechaInicio = datos.FechaInicio;
        FechaFin = datos.FechaFin;
        DocumentoCompra = Guardas.TextoOpcional(datos.DocumentoCompra, "El documento de compra", LongitudMaximaDocumento)?.ToUpperInvariant();
        Costo = datos.Costo;
        Observaciones = Guardas.TextoOpcional(datos.Observaciones, "Las observaciones", LongitudMaximaObservaciones);
    }

    /// <summary>Ocupa un puesto con el equipo. <paramref name="hoy"/> es la fecha actual: no se asigna si ya venció.</summary>
    public void Asignar(Guid activoId, DateOnly hoy)
    {
        if (activoId == Guid.Empty)
        {
            throw new DominioException("Indique el equipo.");
        }

        if (Archivada)
        {
            throw new DominioException("La licencia está archivada.");
        }

        if (FechaFin < hoy)
        {
            throw new DominioException("La licencia venció; renuévela antes de asignar equipos.");
        }

        if (_equipos.Exists(e => e.ActivoId == activoId))
        {
            throw new DominioException("El equipo ya tiene esta licencia.");
        }

        if (_equipos.Count >= Puestos)
        {
            throw new DominioException("La licencia no tiene puestos libres.");
        }

        _equipos.Add(new EquipoLicencia(Id, activoId, hoy));
    }

    /// <summary>Libera el puesto del equipo.</summary>
    public void Quitar(Guid activoId)
    {
        var equipo = _equipos.Find(e => e.ActivoId == activoId)
            ?? throw new DominioException("El equipo no tiene esta licencia.");
        _equipos.Remove(equipo);
    }

    /// <summary>No se renovó: libera todos sus equipos.</summary>
    public void Archivar()
    {
        if (Archivada)
        {
            throw new DominioException("La licencia ya está archivada.");
        }

        Archivada = true;
        _equipos.Clear();
    }

    public void Reactivar()
    {
        if (!Archivada)
        {
            throw new DominioException("La licencia no está archivada.");
        }

        Archivada = false;
    }
}

/// <summary>Un equipo del inventario que ocupa un puesto de la licencia.</summary>
public sealed class EquipoLicencia
{
    // Requerido por EF Core.
    private EquipoLicencia()
    {
    }

    internal EquipoLicencia(Guid licenciaId, Guid activoId, DateOnly asignadoEn)
    {
        LicenciaId = licenciaId;
        ActivoId = activoId;
        AsignadoEn = asignadoEn;
    }

    public Guid LicenciaId { get; private set; }

    public Guid ActivoId { get; private set; }

    public DateOnly AsignadoEn { get; private set; }
}
