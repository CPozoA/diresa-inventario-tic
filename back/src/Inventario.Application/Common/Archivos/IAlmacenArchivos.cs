namespace Inventario.Application.Common.Archivos;

/// <summary>
/// Almacén de archivos (fotos de los activos, D38). Hoy, una carpeta del servidor fuera de la web pública; mañana
/// podría ser otro almacenamiento (Proxmox, objetos) sin tocar el resto del sistema.
/// </summary>
/// <remarks>Las claves son rutas relativas con "/" ("sede/activo/foto.jpg"); el almacén no acepta salir de su carpeta.</remarks>
public interface IAlmacenArchivos
{
    Task GuardarAsync(string clave, ReadOnlyMemory<byte> contenido, CancellationToken cancellationToken);

    /// <summary>El archivo para leer, o nulo si no existe.</summary>
    Task<Stream?> AbrirAsync(string clave, CancellationToken cancellationToken);

    /// <summary>Borra el archivo si existe. No falla si ya no estaba.</summary>
    Task EliminarAsync(string clave, CancellationToken cancellationToken);
}
