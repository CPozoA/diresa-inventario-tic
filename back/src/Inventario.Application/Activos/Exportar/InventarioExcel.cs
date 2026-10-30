namespace Inventario.Application.Activos.Exportar;

/// <summary>Un archivo listo para descargar.</summary>
/// <param name="Contenido">Bytes del archivo.</param>
/// <param name="NombreArchivo">Nombre sugerido, por ejemplo "Inventario-Red-de-Salud-Huanta-2026-10-30.xlsx".</param>
public sealed record ArchivoDescargable(byte[] Contenido, string NombreArchivo);

/// <summary>
/// Datos del inventario para el Excel (D55): ya en texto, números y fechas, para que la infraestructura solo los escriba.
/// </summary>
/// <param name="Ambito">"Red de Salud Huanta" o "Todas las redes".</param>
/// <param name="GeneradoEn">Fecha y hora de la exportación.</param>
/// <param name="Filtros">Filtros aplicados, en palabras ("Establecimiento: C.S. Huanta"…). Vacía sin filtros.</param>
/// <param name="ConRed">Incluye la columna "Red" (superadministrador con todas las redes).</param>
/// <param name="Activos">Una fila por activo, ordenadas por red y código patrimonial.</param>
/// <param name="Discos">Una fila por disco.</param>
/// <param name="ModulosRam">Una fila por módulo de memoria RAM.</param>
public sealed record InventarioParaExcel(
    string Ambito,
    DateTime GeneradoEn,
    IReadOnlyList<string> Filtros,
    bool ConRed,
    IReadOnlyList<ActivoParaExcel> Activos,
    IReadOnlyList<DiscoParaExcel> Discos,
    IReadOnlyList<ModuloRamParaExcel> ModulosRam);

/// <summary>Un activo con todos sus datos, como se escriben en la hoja "Activos".</summary>
public sealed record ActivoParaExcel
{
    public string Red { get; init; } = string.Empty;

    public string CodigoPatrimonial { get; init; } = string.Empty;

    public string TipoActivo { get; init; } = string.Empty;

    public string Marca { get; init; } = string.Empty;

    public string Modelo { get; init; } = string.Empty;

    public string? NumeroSerie { get; init; }

    public string Establecimiento { get; init; } = string.Empty;

    public string Area { get; init; } = string.Empty;

    public string? UbicacionFisica { get; init; }

    /// <summary>Quién lo tiene asignado (D51).</summary>
    public string? Responsable { get; init; }

    public string? DniResponsable { get; init; }

    /// <summary>Texto escrito antes del módulo de asignaciones, si nadie lo tiene asignado (D52).</summary>
    public string? ResponsableAnterior { get; init; }

    public string Estado { get; init; } = string.Empty;

    public string Conservacion { get; init; } = string.Empty;

    public string? Criticidad { get; init; }

    public string? Descripcion { get; init; }

    public string? Procesador { get; init; }

    public string? Generacion { get; init; }

    public int? Nucleos { get; init; }

    /// <summary>Suma de los módulos; nula si no tiene.</summary>
    public int? RamGb { get; init; }

    /// <summary>Suma de los discos; nula si no tiene.</summary>
    public int? AlmacenamientoGb { get; init; }

    public string? Grafica { get; init; }

    public decimal? PantallaPulgadas { get; init; }

    public string? SistemaOperativo { get; init; }

    public string? NombreEquipo { get; init; }

    public string? DominioGrupo { get; init; }

    public string? DireccionIp { get; init; }

    public string? DireccionMac { get; init; }

    public string? Antivirus { get; init; }

    /// <summary>"Sí", "No" o nulo.</summary>
    public string? DiscoCifrado { get; init; }

    public string? Puertos { get; init; }

    public string? Accesorios { get; init; }

    public DateOnly? FechaAdquisicion { get; init; }

    public string? TipoAdquisicion { get; init; }

    public decimal? ValorAdquisicion { get; init; }

    public string? Proveedor { get; init; }

    public string? NumeroOrdenCompra { get; init; }

    public int? VidaUtilAnios { get; init; }

    public DateOnly? FinGarantia { get; init; }

    public string Garantia { get; init; } = string.Empty;

    public DateOnly? FechaRenovacion { get; init; }

    public string Renovacion { get; init; } = string.Empty;

    public DateOnly? ProximoMantenimiento { get; init; }

    public string? Observaciones { get; init; }

    public DateTime RegistradoEn { get; init; }

    public DateTime? ModificadoEn { get; init; }
}

/// <summary>Un disco, con el código del activo para cruzarlo con la hoja "Activos".</summary>
public sealed record DiscoParaExcel(
    string Red, string CodigoPatrimonial, string? Ranura, string Tipo, int CapacidadGb, string Uso, string? MarcaModelo, string? NumeroSerie);

/// <summary>Un módulo de RAM, con el código del activo.</summary>
public sealed record ModuloRamParaExcel(
    string Red, string CodigoPatrimonial, string? Ranura, string Tipo, int CapacidadGb, int? VelocidadMhz, string? MarcaModelo);

/// <summary>Arma el archivo Excel del inventario (la implementación usa ClosedXML, D42).</summary>
public interface IGeneradorExcelInventario
{
    byte[] Generar(InventarioParaExcel datos);
}
