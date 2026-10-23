using System.Globalization;
using Inventario.Application.Common.Seguridad;
using Microsoft.Extensions.Options;

namespace Inventario.Infrastructure.Enlaces;

/// <summary>Sección <c>Aplicacion</c> de appsettings.</summary>
public sealed class OpcionesAplicacion
{
    public const string Seccion = "Aplicacion";

    /// <summary>Dirección del frontend. Se usa para armar los enlaces de los correos.</summary>
    public Uri UrlFrontend { get; set; } = new("https://localhost:4200");
}

internal sealed class EnlacesAplicacion(IOptions<OpcionesAplicacion> opciones) : IEnlacesAplicacion
{
    public Uri RestablecerContrasena(Guid usuarioId, string token)
    {
        var ruta = string.Create(
            CultureInfo.InvariantCulture,
            $"restablecer-contrasena?usuario={usuarioId:D}&token={Uri.EscapeDataString(token)}");

        return new Uri(opciones.Value.UrlFrontend, ruta);
    }
}
