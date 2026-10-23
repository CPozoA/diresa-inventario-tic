using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;

namespace Inventario.Api.Seguridad;

internal static class ConfiguracionSeguridad
{
    /// <summary>Límite de peticiones para iniciar sesión y recuperar contraseña.</summary>
    public const string PoliticaAutenticacion = "autenticacion";

    /// <summary>
    /// Cookie de sesión, autorización y límite de intentos. Detalle en docs/ARQUITECTURA.md §6.
    /// </summary>
    public static IServiceCollection AddSeguridadHttp(this IServiceCollection services)
    {
        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(opciones =>
        {
            opciones.Cookie.Name = "InventarioTIC.Sesion";
            opciones.Cookie.HttpOnly = true;                         // JavaScript no puede leerla
            opciones.Cookie.SecurePolicy = CookieSecurePolicy.Always; // solo por HTTPS
            opciones.Cookie.SameSite = SameSiteMode.Strict;          // no viaja en peticiones desde otros sitios

            // Vence a los 30 minutos sin actividad; cada petición la renueva.
            opciones.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            opciones.SlidingExpiration = true;

            // Es una API: en lugar de redirigir a una página de login, responde 401 o 403.
            opciones.Events.OnRedirectToLogin = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            opciones.Events.OnRedirectToAccessDenied = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        // La exigencia de sesión se aplica al grupo /api en Program.cs: todo endpoint de la API
        // exige sesión salvo que se marque explícitamente con AllowAnonymous().
        services.AddAuthorization();

        // Máximo 10 intentos por minuto por dirección IP en los endpoints de autenticación.
        services.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opciones.AddPolicy(PoliticaAutenticacion, contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        return services;
    }
}
