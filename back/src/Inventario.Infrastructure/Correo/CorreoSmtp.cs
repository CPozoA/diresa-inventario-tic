using System.Net;
using System.Net.Mail;
using Inventario.Application.Common.Correo;
using Microsoft.Extensions.Options;

namespace Inventario.Infrastructure.Correo;

/// <summary>
/// Envía correos por SMTP (el que ofrece el hosting, por ejemplo SmarterASP.NET).
/// Usa el cliente SMTP de .NET con STARTTLS; si más adelante se necesita, se puede cambiar por MailKit
/// sin tocar los casos de uso, porque estos dependen solo de <see cref="IServicioCorreo"/>.
/// </summary>
internal sealed class CorreoSmtp(IOptions<OpcionesCorreo> opciones) : IServicioCorreo
{
    public async Task EnviarAsync(MensajeCorreo mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var o = opciones.Value;

        if (string.IsNullOrWhiteSpace(o.Servidor) || string.IsNullOrWhiteSpace(o.Remitente))
        {
            throw new InvalidOperationException("Falta configurar Correo:Servidor y Correo:Remitente.");
        }

        using var correo = new MailMessage
        {
            From = new MailAddress(o.Remitente, o.NombreRemitente),
            Subject = mensaje.Asunto,
            Body = mensaje.CuerpoHtml,
            IsBodyHtml = true,
        };
        correo.To.Add(mensaje.Para);

        using var cliente = new SmtpClient(o.Servidor, o.Puerto)
        {
            EnableSsl = o.UsarSsl,
            Credentials = string.IsNullOrEmpty(o.Usuario) ? null : new NetworkCredential(o.Usuario, o.Contrasena),
        };

        await cliente.SendMailAsync(correo, cancellationToken);
    }
}
