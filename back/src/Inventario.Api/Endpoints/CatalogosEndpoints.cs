using Inventario.Api.Common;
using Inventario.Application.Catalogos;
using Inventario.Application.Common.Mensajeria;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>
/// Catálogos (listas de valores). Una sola API para todos: <c>/api/catalogos/{tipo}</c>, con tipo = areas,
/// tipos-activo, marcas, estados-activo, sistemas-operativos o tipos-licencia. Consultar: cualquier usuario con sesión.
/// Modificar: Catalogos.GestionarSede (áreas) o Catalogos.GestionarGlobales (los demás); lo valida Application.
/// </summary>
internal static class CatalogosEndpoints
{
    public static IEndpointRouteBuilder MapCatalogosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/catalogos").WithTags("Catalogos");

        grupo.MapGet("/", ListarTiposAsync)
            .WithName("ListarTiposCatalogo")
            .WithSummary("Catálogos disponibles y si quien consulta puede modificarlos.");

        grupo.MapGet("/{tipo}", ListarAsync)
            .WithName("ListarElementosCatalogo")
            .WithSummary("Elementos de un catálogo. En áreas, el superadministrador indica ?sedeId=.");

        grupo.MapPost("/{tipo}", CrearAsync)
            .WithName("CrearElementoCatalogo")
            .WithSummary("Agrega un elemento al catálogo.");

        grupo.MapPut("/{tipo}/{id:guid}", RenombrarAsync)
            .WithName("RenombrarElementoCatalogo")
            .WithSummary("Cambia el nombre de un elemento.");

        grupo.MapPost("/{tipo}/{id:guid}/activar", ActivarAsync)
            .WithName("ActivarElementoCatalogo")
            .WithSummary("Vuelve a ofrecer el elemento al registrar.");

        grupo.MapPut("/tipos-activo/{id:guid}/ficha", CambiarFichaAsync)
            .RequirePermiso(Permisos.Catalogos.GestionarGlobales)
            .WithName("CambiarFichaTipoActivo")
            .WithSummary("Cambia qué características técnicas lleva un tipo de activo: Computo, Red o Ninguna.");

        grupo.MapPost("/{tipo}/{id:guid}/desactivar", DesactivarAsync)
            .WithName("DesactivarElementoCatalogo")
            .WithSummary("Deja de ofrecer el elemento al registrar; se conserva en los registros que ya lo usan.");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<TipoCatalogoDto>>, ProblemHttpResult>> ListarTiposAsync(
        IQueryHandler<ListarTiposCatalogoQuery, IReadOnlyList<TipoCatalogoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarTiposCatalogoQuery(), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ElementoCatalogoDto>>, ProblemHttpResult>> ListarAsync(
        string tipo,
        Guid? sedeId,
        bool? incluirInactivos,
        IQueryHandler<ListarElementosCatalogoQuery, IReadOnlyList<ElementoCatalogoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarElementosCatalogoQuery(tipo, sedeId, incluirInactivos ?? false), cancellationToken))
            .ToHttpResult();

    private static async Task<Results<Created<IdCreado>, ProblemHttpResult>> CrearAsync(
        string tipo,
        CrearElementoCatalogoRequest cuerpo,
        ICommandHandler<CrearElementoCatalogoCommand, IdCreado> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CrearElementoCatalogoCommand(tipo, cuerpo.SedeId, cuerpo.Nombre), cancellationToken))
            .ToCreatedResult(c => $"/api/catalogos/{tipo}/{c.Id}");

    private static async Task<Results<NoContent, ProblemHttpResult>> RenombrarAsync(
        string tipo,
        Guid id,
        RenombrarElementoCatalogoRequest cuerpo,
        ICommandHandler<RenombrarElementoCatalogoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new RenombrarElementoCatalogoCommand(tipo, id, cuerpo.Nombre), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> ActivarAsync(
        string tipo,
        Guid id,
        ICommandHandler<CambiarEstadoElementoCatalogoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CambiarEstadoElementoCatalogoCommand(tipo, id, Activo: true), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DesactivarAsync(
        string tipo,
        Guid id,
        ICommandHandler<CambiarEstadoElementoCatalogoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CambiarEstadoElementoCatalogoCommand(tipo, id, Activo: false), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> CambiarFichaAsync(
        Guid id,
        CambiarFichaTipoActivoRequest cuerpo,
        ICommandHandler<CambiarFichaTipoActivoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CambiarFichaTipoActivoCommand(id, cuerpo.Ficha), cancellationToken)).ToNoContentResult();
}

internal sealed record CrearElementoCatalogoRequest(Guid? SedeId, string Nombre);

internal sealed record CambiarFichaTipoActivoRequest(FichaTecnica Ficha);

internal sealed record RenombrarElementoCatalogoRequest(string Nombre);
