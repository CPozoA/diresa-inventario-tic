namespace Inventario.Api.Endpoints;

/// <summary>Comprobación rápida de que la API está en marcha.</summary>
internal static class SaludEndpoints
{
    public static IEndpointRouteBuilder MapSaludEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/salud", (IHostEnvironment entorno, TimeProvider reloj) => TypedResults.Ok(new RespuestaSalud(
                Estado: "ok",
                Entorno: entorno.EnvironmentName,
                Version: typeof(SaludEndpoints).Assembly.GetName().Version?.ToString() ?? "desconocida",
                FechaServidor: reloj.GetUtcNow())))
            .AllowAnonymous()
            .WithName("ObtenerSalud")
            .WithTags("Salud");

        return app;
    }

    internal sealed record RespuestaSalud(string Estado, string Entorno, string Version, DateTimeOffset FechaServidor);
}
