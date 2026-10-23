using Microsoft.AspNetCore.Antiforgery;

namespace Inventario.Api.Seguridad;

/// <summary>
/// Protección contra CSRF (docs/ARQUITECTURA.md D24) con el patrón "doble envío" que Angular usa por defecto:
/// la API entrega un token en la cookie <c>XSRF-TOKEN</c> (legible por JavaScript del mismo sitio) y exige que
/// vuelva en la cabecera <c>X-XSRF-TOKEN</c> en toda petición que cambia datos. Otro sitio no puede leer la cookie,
/// así que no puede armar la cabecera. El token queda ligado al usuario: al iniciar o cerrar sesión hay que pedir otro.
/// </summary>
internal static class ProteccionCsrf
{
    /// <summary>Cookie que lee el frontend. No es HttpOnly a propósito: Angular necesita leerla.</summary>
    public const string CookieToken = "XSRF-TOKEN";

    /// <summary>Cabecera donde el frontend devuelve el token.</summary>
    public const string Cabecera = "X-XSRF-TOKEN";

    public const string CodigoError = "Seguridad.TokenCsrfInvalido";

    public static IServiceCollection AddProteccionCsrf(this IServiceCollection services) =>
        services.AddAntiforgery(opciones =>
        {
            opciones.HeaderName = Cabecera;

            // Cookie interna de ASP.NET Core con la otra mitad del token: HttpOnly, solo HTTPS y solo este sitio.
            opciones.Cookie.Name = "InventarioTIC.Antiforgery";
            opciones.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            opciones.Cookie.SameSite = SameSiteMode.Strict;
        });

    /// <summary>Genera un token para el usuario actual (o anónimo) y lo deja en la cookie <see cref="CookieToken"/>.</summary>
    public static string EmitirToken(IAntiforgery antiforgery, HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(antiforgery);
        ArgumentNullException.ThrowIfNull(contexto);

        var tokens = antiforgery.GetAndStoreTokens(contexto);
        var token = tokens.RequestToken
            ?? throw new InvalidOperationException("No se pudo generar el token antifalsificación.");

        contexto.Response.Cookies.Append(CookieToken, token, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });

        return token;
    }
}

/// <summary>
/// Exige un token antifalsificación válido en toda petición a <c>/api</c> que cambia datos (POST, PUT, PATCH, DELETE),
/// incluidas iniciar sesión y recuperar contraseña. Las consultas (GET) no lo necesitan: no cambian nada.
/// </summary>
internal sealed partial class ValidarCsrfMiddleware(
    RequestDelegate siguiente,
    IAntiforgery antiforgery,
    ILogger<ValidarCsrfMiddleware> registro)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        var peticion = contexto.Request;

        if (peticion.Path.StartsWithSegments("/api") && CambiaDatos(peticion.Method)
            && !await antiforgery.IsRequestValidAsync(contexto))
        {
            var ruta = peticion.Path.Value ?? string.Empty;
            RegistrarRechazo(registro, peticion.Method, ruta);

            await TypedResults.Problem(
                    title: "La solicitud no pudo verificarse. Recargue la página e inténtelo de nuevo.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["codigo"] = ProteccionCsrf.CodigoError })
                .ExecuteAsync(contexto);
            return;
        }

        await siguiente(contexto);
    }

    private static bool CambiaDatos(string metodo) =>
        HttpMethods.IsPost(metodo) || HttpMethods.IsPut(metodo) || HttpMethods.IsPatch(metodo) || HttpMethods.IsDelete(metodo);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Petición rechazada por token antifalsificación inválido: {Metodo} {Ruta}")]
    private static partial void RegistrarRechazo(ILogger registro, string metodo, string ruta);
}
