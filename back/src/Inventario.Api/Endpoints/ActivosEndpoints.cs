using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Activos;
using Inventario.Application.Activos.Consultar;
using Inventario.Application.Activos.Gestionar;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>
/// Activos (fichas técnicas, docs/ARQUITECTURA.md §5.10). Consultar: Activos.Ver. Registrar: Activos.Crear.
/// Modificar: Activos.Editar. Eliminar: Activos.Eliminar. Cada usuario de sede solo ve y toca los de su sede.
/// </summary>
internal static class ActivosEndpoints
{
    public static IEndpointRouteBuilder MapActivosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/activos").WithTags("Activos");

        grupo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ListarActivos")
            .WithSummary("Inventario paginado, con filtros. ?buscar= código patrimonial, serie, modelo, marca, IP, nombre de equipo, o nombre o DNI de quien lo tiene asignado. ?atencion= y ?falta= como en el resumen (sin los de baja).");

        grupo.MapGet("/listas", ListasAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ObtenerListasActivo")
            .WithSummary("Listas fijas de la ficha: conservación, criticidad, tipos y capacidades de discos y RAM…");

        grupo.MapGet("/serie", BuscarSerieAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("BuscarSerieActivo")
            .WithSummary("Activos de la sede que ya usan ese número de serie (para avisar antes de guardar).");

        grupo.MapGet("/sugerencias", SugerenciasAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("SugerenciasActivo")
            .WithSummary("Valores ya registrados en la red para un campo escrito (modelo, proveedor, dominio…), los más usados primero.");

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ObtenerFichaActivo")
            .WithSummary("Ficha técnica completa, con discos, RAM y la situación a hoy.");

        grupo.MapPost("/", CrearAsync)
            .RequirePermiso(Permisos.Activos.Crear)
            .WithName("CrearActivo")
            .WithSummary("Registra un activo. La sede es la del establecimiento.");

        grupo.MapPut("/{id:guid}", EditarAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("EditarActivo")
            .WithSummary("Reemplaza la ficha, incluidos discos y RAM. El activo no cambia de sede.");

        grupo.MapDelete("/{id:guid}", EliminarAsync)
            .RequirePermiso(Permisos.Activos.Eliminar)
            .WithName("EliminarActivo")
            .WithSummary("Elimina un activo registrado por error.");

        return app;
    }

    private static async Task<Results<Ok<PaginaActivosDto>, ProblemHttpResult>> ListarAsync(
        Guid? sedeId,
        Guid? establecimientoId,
        Guid? tipoActivoId,
        Guid? areaId,
        Guid? estadoActivoId,
        EstadoGarantia? garantia,
        SituacionRenovacion? renovacion,
        MotivoAtencion? atencion,
        DatoFaltante? falta,
        string? buscar,
        int? pagina,
        int? tamanoPagina,
        IQueryHandler<ListarActivosQuery, PaginaActivosDto> handler,
        CancellationToken cancellationToken)
    {
        var consulta = new ListarActivosQuery(
            sedeId, establecimientoId, tipoActivoId, areaId, estadoActivoId, garantia, renovacion, atencion, falta, buscar, pagina, tamanoPagina);
        return (await handler.Handle(consulta, cancellationToken)).ToHttpResult();
    }

    private static async Task<Results<Ok<ListasActivoDto>, ProblemHttpResult>> ListasAsync(
        IQueryHandler<ObtenerListasActivoQuery, ListasActivoDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerListasActivoQuery(), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<ActivoConSerieDto>>, ProblemHttpResult>> BuscarSerieAsync(
        string? numero,
        Guid? excluirId,
        IQueryHandler<BuscarSerieActivoQuery, IReadOnlyList<ActivoConSerieDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new BuscarSerieActivoQuery(numero, excluirId), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<string>>, ProblemHttpResult>> SugerenciasAsync(
        CampoSugerencia campo,
        string? texto,
        Guid? marcaId,
        Guid? sistemaOperativoId,
        Guid? establecimientoId,
        IQueryHandler<SugerenciasActivoQuery, IReadOnlyList<string>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new SugerenciasActivoQuery(campo, texto, marcaId, sistemaOperativoId, establecimientoId), cancellationToken))
            .ToHttpResult();

    private static async Task<Results<Ok<FichaActivoDto>, ProblemHttpResult>> ObtenerAsync(
        Guid id,
        IQueryHandler<ObtenerFichaActivoQuery, FichaActivoDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerFichaActivoQuery(id), cancellationToken)).ToHttpResult();

    private static async Task<Results<Created<IdCreado>, ProblemHttpResult>> CrearAsync(
        FichaActivo ficha,
        ICommandHandler<CrearActivoCommand, IdCreado> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CrearActivoCommand(ficha), cancellationToken)).ToCreatedResult(c => $"/api/activos/{c.Id}");

    private static async Task<Results<NoContent, ProblemHttpResult>> EditarAsync(
        Guid id,
        FichaActivo ficha,
        ICommandHandler<EditarActivoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new EditarActivoCommand(id, ficha), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> EliminarAsync(
        Guid id,
        ICommandHandler<EliminarActivoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new EliminarActivoCommand(id), cancellationToken)).ToNoContentResult();
}
