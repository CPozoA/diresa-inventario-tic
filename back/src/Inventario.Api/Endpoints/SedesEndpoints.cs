using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Sedes.CambiarEstado;
using Inventario.Application.Sedes.Crear;
using Inventario.Application.Sedes.Editar;
using Inventario.Application.Sedes.Listar;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>Sedes. Todo exige Sedes.Gestionar: solo el superadministrador.</summary>
internal static class SedesEndpoints
{
    public static IEndpointRouteBuilder MapSedesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/sedes")
            .WithTags("Sedes")
            .RequirePermiso(Permisos.Sedes.Gestionar);

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarSedes")
            .WithSummary("Lista las sedes ordenadas por nombre.");

        grupo.MapPost("/", CrearAsync)
            .WithName("CrearSede")
            .WithSummary("Crea una sede activa, con los roles iniciales Editor y Consulta.");

        grupo.MapPut("/{id:guid}", EditarAsync)
            .WithName("EditarSede")
            .WithSummary("Cambia el nombre de una sede. El código no cambia.");

        grupo.MapPost("/{id:guid}/activar", ActivarAsync)
            .WithName("ActivarSede")
            .WithSummary("Activa una sede: sus usuarios vuelven a poder ingresar.");

        grupo.MapPost("/{id:guid}/desactivar", DesactivarAsync)
            .WithName("DesactivarSede")
            .WithSummary("Desactiva una sede: conserva sus datos, pero sus usuarios no pueden ingresar y se cierran sus sesiones.");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<SedeDto>>, ProblemHttpResult>> ListarAsync(
        IQueryHandler<ListarSedesQuery, IReadOnlyList<SedeDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarSedesQuery(), cancellationToken)).ToHttpResult();

    private static async Task<Results<Created<IdCreado>, ProblemHttpResult>> CrearAsync(
        CrearSedeCommand comando,
        ICommandHandler<CrearSedeCommand, IdCreado> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(comando, cancellationToken)).ToCreatedResult(c => $"/api/sedes/{c.Id}");

    private static async Task<Results<NoContent, ProblemHttpResult>> EditarAsync(
        Guid id,
        EditarSedeRequest cuerpo,
        ICommandHandler<EditarSedeCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new EditarSedeCommand(id, cuerpo.Nombre), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> ActivarAsync(
        Guid id,
        ICommandHandler<CambiarEstadoSedeCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CambiarEstadoSedeCommand(id, Activa: true), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DesactivarAsync(
        Guid id,
        ICommandHandler<CambiarEstadoSedeCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CambiarEstadoSedeCommand(id, Activa: false), cancellationToken)).ToNoContentResult();
}

internal sealed record EditarSedeRequest(string Nombre);
