using Inventario.Application.Common.Archivos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventario.Infrastructure.Archivos;

/// <summary>
/// Almacén de archivos en una carpeta del servidor (D38). La carpeta no se publica en la web: los archivos solo salen
/// por la API, después de comprobar la sesión, el permiso y la sede.
/// </summary>
internal sealed partial class AlmacenArchivosEnDisco(
    IOptions<OpcionesAlmacenamiento> opciones,
    IHostEnvironment entorno,
    ILogger<AlmacenArchivosEnDisco> logger) : IAlmacenArchivos
{
    private string Raiz => Path.GetFullPath(Path.Combine(entorno.ContentRootPath, opciones.Value.CarpetaFotos));

    public async Task GuardarAsync(string clave, ReadOnlyMemory<byte> contenido, CancellationToken cancellationToken)
    {
        var ruta = Ruta(clave);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);

        // Se escribe en un archivo temporal y se renombra: nunca queda un archivo a medio escribir con el nombre final.
        var temporal = ruta + ".tmp";
        await File.WriteAllBytesAsync(temporal, contenido.ToArray(), cancellationToken);
        File.Move(temporal, ruta, overwrite: true);
    }

    public Task<Stream?> AbrirAsync(string clave, CancellationToken cancellationToken)
    {
        var ruta = Ruta(clave);
        Stream? flujo = File.Exists(ruta)
            ? new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true)
            : null;
        return Task.FromResult(flujo);
    }

    public Task EliminarAsync(string clave, CancellationToken cancellationToken)
    {
        var ruta = Ruta(clave);
        try
        {
            File.Delete(ruta);
        }
        catch (IOException error)
        {
            // Un archivo que no se pudo borrar no impide la operación: queda sin uso y se registra.
            LogNoSeBorro(logger, error, clave);
        }
        catch (UnauthorizedAccessException error)
        {
            LogNoSeBorro(logger, error, clave);
        }

        return Task.CompletedTask;
    }

    /// <summary>Ruta completa de una clave, comprobando que no salga de la carpeta del almacén.</summary>
    private string Ruta(string clave)
    {
        if (string.IsNullOrWhiteSpace(clave) || clave.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(clave))
        {
            throw new ArgumentException("La clave del archivo no es válida.", nameof(clave));
        }

        var raiz = Raiz;
        var ruta = Path.GetFullPath(Path.Combine(raiz, clave.Replace('/', Path.DirectorySeparatorChar)));
        if (!ruta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La clave del archivo no es válida.", nameof(clave));
        }

        return ruta;
    }

    [LoggerMessage(EventId = 1201, Level = LogLevel.Warning, Message = "No se pudo borrar el archivo {Clave} del almacén.")]
    private static partial void LogNoSeBorro(ILogger logger, Exception error, string clave);
}
