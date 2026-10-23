namespace Inventario.Infrastructure.Correo;

/// <summary>Sección <c>Correo</c> de appsettings.</summary>
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    /// <summary>
    /// <c>Carpeta</c>: guarda cada correo como archivo HTML (desarrollo; no envía nada).
    /// <c>Smtp</c>: envía por el servidor SMTP configurado (producción).
    /// </summary>
    public string Modo { get; set; } = "Smtp";

    /// <summary>Carpeta para el modo <c>Carpeta</c>, relativa al proyecto Api.</summary>
    public string Carpeta { get; set; } = "correos-dev";

    public string Remitente { get; set; } = string.Empty;

    public string NombreRemitente { get; set; } = "Inventario TIC";

    public string Servidor { get; set; } = string.Empty;

    public int Puerto { get; set; } = 587;

    /// <summary>Usa STARTTLS. Mantener en verdadero salvo que el proveedor indique lo contrario.</summary>
    public bool UsarSsl { get; set; } = true;

    public string Usuario { get; set; } = string.Empty;

    /// <summary>Nunca en el repositorio: va en user-secrets (desarrollo) o en variables de entorno (servidor).</summary>
    public string Contrasena { get; set; } = string.Empty;
}
