namespace Inventario.Application.Establecimientos.Importar;

/// <summary>Una fila de datos del Excel, con los valores como texto tal como venían (sin interpretar).</summary>
/// <param name="NumeroFila">Número de la fila en la hoja (para informar errores).</param>
/// <param name="IdEstablecimiento">Columna Id_Establecimiento.</param>
/// <param name="CodigoUnico">Columna Codigo_Unico.</param>
/// <param name="Nombre">Columna Nombre.</param>
/// <param name="Microred">Columna Microred.</param>
/// <param name="Red">Columna Red: se convierte en la sede.</param>
/// <param name="Provincia">Columna Provincia.</param>
/// <param name="Distrito">Columna Distrito.</param>
/// <param name="Ubigeo">Columna Ubigeo.</param>
/// <param name="Categoria">Columna Categoria.</param>
public sealed record FilaEstablecimiento(
    int NumeroFila,
    string? IdEstablecimiento,
    string? CodigoUnico,
    string? Nombre,
    string? Microred,
    string? Red,
    string? Provincia,
    string? Distrito,
    string? Ubigeo,
    string? Categoria);

/// <summary>Encabezados de la hoja de establecimientos.</summary>
public static class ColumnasEstablecimientos
{
    /// <summary>Columnas de la plantilla, en su orden.</summary>
    public static IReadOnlyList<string> Todas { get; } =
        ["Id_Establecimiento", "Codigo_Unico", "Nombre", "Categoria", "Microred", "Red", "Provincia", "Distrito", "Ubigeo"];

    /// <summary>Columnas que no pueden faltar en el archivo.</summary>
    public static IReadOnlyList<string> Obligatorias { get; } = ["Id_Establecimiento", "Codigo_Unico", "Nombre", "Red"];
}

/// <summary>Resultado de leer el archivo: sus filas, o el motivo por el que no se pudo leer.</summary>
public sealed record LecturaEstablecimientos(IReadOnlyList<FilaEstablecimiento> Filas, string? Error)
{
    public static LecturaEstablecimientos ConError(string error) => new([], error);
}

/// <summary>
/// Lee y genera la hoja de Excel de establecimientos (docs/ARQUITECTURA.md §5.9, D42).
/// La implementa Infrastructure con ClosedXML; Application no depende de la librería.
/// </summary>
public interface IHojaEstablecimientos
{
    /// <summary>Lee la primera hoja. Las columnas se reconocen por su encabezado, en cualquier orden.</summary>
    LecturaEstablecimientos Leer(Stream archivo);

    /// <summary>Plantilla vacía (.xlsx) con los encabezados y una hoja de instrucciones.</summary>
    byte[] CrearPlantilla();
}
