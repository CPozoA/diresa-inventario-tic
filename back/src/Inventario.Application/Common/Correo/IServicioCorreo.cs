namespace Inventario.Application.Common.Correo;

/// <summary>Mensaje de correo en HTML.</summary>
public sealed record MensajeCorreo(string Para, string Asunto, string CuerpoHtml);

/// <summary>
/// Envío de correo. En desarrollo se guarda como archivo; en producción se envía por SMTP
/// (se elige con <c>Correo:Modo</c> en appsettings).
/// </summary>
public interface IServicioCorreo
{
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancellationToken);
}
