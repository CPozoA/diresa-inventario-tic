using System.Globalization;
using System.Net;
using Inventario.Application.Common.Correo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventario.Infrastructure.Correo;

/// <summary>
/// Para desarrollo: en lugar de enviar, guarda cada correo como un archivo HTML que se abre en el navegador.
/// Así se prueba la recuperación de contraseña sin un servidor de correo.
/// </summary>
internal sealed partial class CorreoEnCarpeta(
    IOptions<OpcionesCorreo> opciones,
    IHostEnvironment entorno,
    TimeProvider reloj,
    ILogger<CorreoEnCarpeta> logger) : IServicioCorreo
{
    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        var carpeta = Path.Combine(entorno.ContentRootPath, opciones.Value.Carpeta);
        Directory.CreateDirectory(carpeta);

        var marca = reloj.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var archivo = Path.Combine(carpeta, $"{marca}-{Guid.NewGuid():N}.html");

        var contenido = $"""
            <!doctype html>
            <meta charset="utf-8">
            <p><strong>Para:</strong> {WebUtility.HtmlEncode(mensaje.Para)}<br>
            <strong>Asunto:</strong> {WebUtility.HtmlEncode(mensaje.Asunto)}</p>
            <hr>
            {mensaje.CuerpoHtml}
            """;

        await File.WriteAllTextAsync(archivo, contenido, cancellationToken);
        LogCorreoGuardado(logger, archivo);
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Correo de desarrollo guardado en {Archivo}")]
    private static partial void LogCorreoGuardado(ILogger logger, string archivo);
}
