using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Cuenta;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>Sesión y contraseñas del propio usuario.</summary>
internal static class CuentaEndpoints
{
    public static IEndpointRouteBuilder MapCuentaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/cuenta").WithTags("Cuenta");

        grupo.MapPost("/iniciar-sesion", IniciarSesionAsync)
            .AllowAnonymous()
            .RequireRateLimiting(ConfiguracionSeguridad.PoliticaAutenticacion)
            .WithName("IniciarSesion")
            .WithSummary("Inicia sesión con usuario (o correo) y contraseña. Al tercer intento fallido bloquea la cuenta 15 minutos.");

        grupo.MapPost("/cerrar-sesion", CerrarSesionAsync)
            .WithName("CerrarSesion")
            .WithSummary("Cierra la sesión actual.");

        grupo.MapGet("/sesion", ObtenerSesionAsync)
            .WithName("ObtenerSesion")
            .WithSummary("Datos del usuario conectado: sede, permisos y si debe cambiar la contraseña.");

        grupo.MapPost("/cambiar-contrasena", CambiarContrasenaAsync)
            .WithName("CambiarContrasena")
            .WithSummary("Cambia la contraseña del usuario conectado.");

        grupo.MapPost("/olvide-contrasena", SolicitarRestablecimientoAsync)
            .AllowAnonymous()
            .RequireRateLimiting(ConfiguracionSeguridad.PoliticaAutenticacion)
            .WithName("OlvideContrasena")
            .WithSummary("Envía un enlace para restablecer la contraseña. Responde igual exista o no el correo.");

        grupo.MapPost("/restablecer-contrasena", RestablecerContrasenaAsync)
            .AllowAnonymous()
            .RequireRateLimiting(ConfiguracionSeguridad.PoliticaAutenticacion)
            .WithName("RestablecerContrasena")
            .WithSummary("Define una contraseña nueva con el enlace recibido por correo.");

        return app;
    }

    private static async Task<Results<Ok<SesionDto>, ProblemHttpResult>> IniciarSesionAsync(
        IniciarSesionCommand comando,
        ICommandHandler<IniciarSesionCommand, SesionDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(comando, cancellationToken)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> CerrarSesionAsync(
        ICommandHandler<CerrarSesionCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CerrarSesionCommand(), cancellationToken)).ToNoContentResult();

    private static async Task<Results<Ok<SesionDto>, ProblemHttpResult>> ObtenerSesionAsync(
        IQueryHandler<ObtenerSesionQuery, SesionDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerSesionQuery(), cancellationToken)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> CambiarContrasenaAsync(
        CambiarContrasenaCommand comando,
        ICommandHandler<CambiarContrasenaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(comando, cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> SolicitarRestablecimientoAsync(
        SolicitarRestablecimientoCommand comando,
        ICommandHandler<SolicitarRestablecimientoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(comando, cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> RestablecerContrasenaAsync(
        RestablecerContrasenaCommand comando,
        ICommandHandler<RestablecerContrasenaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(comando, cancellationToken)).ToNoContentResult();
}
