namespace Inventario.Infrastructure.Archivos;

/// <summary>Dónde se guardan las fotos de los activos (D38). Sección <c>Almacenamiento</c> de la configuración.</summary>
public sealed class OpcionesAlmacenamiento
{
    public const string Seccion = "Almacenamiento";

    /// <summary>
    /// Carpeta de las fotos. Relativa al proyecto Api en desarrollo (<c>fotos-dev</c>); en el servidor, una ruta absoluta
    /// fuera de la carpeta publicada (por ejemplo <c>D:\InventarioTIC\fotos</c>), incluida en los respaldos.
    /// </summary>
    public string CarpetaFotos { get; set; } = "fotos-dev";
}
