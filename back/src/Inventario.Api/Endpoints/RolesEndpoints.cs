using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Roles.Crear;
using Inventario.Application.Roles.Editar;
using Inventario.Application.Roles.Eliminar;
using Inventario.Application.Roles.Listar;
using Inventario.Application.Roles.ListarPermisos;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>Roles de sede. Las reglas contra el escalamiento de privilegios están en Application (ReglasDeGestion).</summary>
internal static class RolesEndpoints
{
    public static IEndpointRouteBuilder MapRolesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/roles").WithTags("Roles");

        grupo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Roles.Ver)
            .WithName("ListarRoles")
            .WithSummary("Roles asignables en una sede. El superadministrador indica la sede con ?sedeId=.");

        grupo.MapGet("/permisos", ListarPermisosAsync)
            .RequirePermiso(Permisos.Roles.Ver)
            .WithName("ListarPermisos")
            .WithSummary("Permisos que se pueden combinar en un rol, y si quien consulta puede otorgarlos.");

        grupo.MapPost("/", CrearAsync)
            .RequirePermiso(Permisos.Roles.Gestionar)
            .WithName("CrearRol")
            .WithSummary("Crea un rol de sede. Solo con permisos que quien lo crea ya tiene.");

        grupo.MapPut("/{id:guid}", EditarAsync)
            .RequirePermiso(Permisos.Roles.Gestionar)
            .WithName("EditarRol")
            .WithSummary("Cambia nombre, descripción y permisos de un rol de sede.");

        grupo.MapDelete("/{id:guid}", EliminarAsync)
            .RequirePermiso(Permisos.Roles.Gestionar)
            .WithName("EliminarRol")
            .WithSummary("Elimina un rol de sede que no esté asignado a nadie.");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<RolDto>>, ProblemHttpResult>> ListarAsync(
        Guid? sedeId,
        IQueryHandler<ListarRolesQuery, IReadOnlyList<RolDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarRolesQuery(sedeId), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<PermisoDto>>, ProblemHttpResult>> ListarPermisosAsync(
        IQueryHandler<ListarPermisosQuery, IReadOnlyList<PermisoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarPermisosQuery(), cancellationToken)).ToHttpResult();

    private static async Task<Results<Created<IdCreado>, ProblemHttpResult>> CrearAsync(
        CrearRolCommand comando,
        ICommandHandler<CrearRolCommand, IdCreado> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(comando, cancellationToken)).ToCreatedResult(c => $"/api/roles/{c.Id}");

    private static async Task<Results<NoContent, ProblemHttpResult>> EditarAsync(
        Guid id,
        EditarRolRequest cuerpo,
        ICommandHandler<EditarRolCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new EditarRolCommand(id, cuerpo.Nombre, cuerpo.Descripcion, cuerpo.Permisos), cancellationToken))
            .ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> EliminarAsync(
        Guid id,
        ICommandHandler<EliminarRolCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new EliminarRolCommand(id), cancellationToken)).ToNoContentResult();
}

internal sealed record EditarRolRequest(string Nombre, string? Descripcion, IReadOnlyList<string>? Permisos);
