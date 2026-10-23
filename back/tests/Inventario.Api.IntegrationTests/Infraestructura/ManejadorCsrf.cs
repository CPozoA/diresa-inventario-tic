using System.Net;

namespace Inventario.Api.IntegrationTests.Infraestructura;

/// <summary>
/// Hace con el token antifalsificación lo mismo que hará Angular: lo pide si aún no lo tiene, lo envía en la cabecera
/// X-XSRF-TOKEN en cada POST, PUT o DELETE, y pide uno nuevo después de iniciar o cerrar sesión (el token está ligado al usuario).
/// </summary>
public sealed class ManejadorCsrf(CookieContainer cookies) : DelegatingHandler
{
    private const string Cookie = "XSRF-TOKEN";
    private const string Cabecera = "X-XSRF-TOKEN";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uri = request.RequestUri ?? throw new InvalidOperationException("La petición no tiene dirección.");

        if (CambiaDatos(request.Method))
        {
            var token = Leer(uri) ?? await PedirTokenAsync(uri, cancellationToken);
            request.Headers.Remove(Cabecera);
            request.Headers.Add(Cabecera, token);
        }

        var respuesta = await base.SendAsync(request, cancellationToken);

        var ruta = uri.AbsolutePath;
        if (respuesta.IsSuccessStatusCode
            && (ruta.EndsWith("/cuenta/iniciar-sesion", StringComparison.Ordinal)
                || ruta.EndsWith("/cuenta/cerrar-sesion", StringComparison.Ordinal)))
        {
            await PedirTokenAsync(uri, cancellationToken);
        }

        return respuesta;
    }

    private static bool CambiaDatos(HttpMethod metodo) =>
        metodo == HttpMethod.Post || metodo == HttpMethod.Put || metodo == HttpMethod.Patch || metodo == HttpMethod.Delete;

    private string? Leer(Uri uri) => cookies.GetCookies(uri)[Cookie]?.Value;

    private async Task<string> PedirTokenAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, new Uri(uri, "/api/cuenta/csrf"));
        using var respuesta = await base.SendAsync(peticion, cancellationToken);
        respuesta.EnsureSuccessStatusCode();

        return Leer(uri) ?? throw new InvalidOperationException("La API no entregó la cookie XSRF-TOKEN.");
    }
}
