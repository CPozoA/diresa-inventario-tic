using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Asignaciones;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>
/// Asignaciones de los activos al personal de la red (D51, docs/ARQUITECTURA.md §5.12). Consultar: Activos.Ver.
/// Asignar, reasignar y registrar la devolución: Activos.Editar.
/// </summary>
internal static class AsignacionesEndpoints
{
    public static IEndpointRouteBuilder MapAsignacionesEndpoints(this IEndpointRouteBuilder app)
    {
        var activo = app.MapGroup("/activos/{id:guid}/asignaciones").WithTags("Asignaciones");

        activo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ListarAsignacionesActivo")
            .WithSummary("Historial de asignaciones del activo: la vigente primero.");

        activo.MapPost("/", AsignarAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("AsignarActivo")
            .WithSummary("Entrega el activo a una persona de la red. Si ya lo tenía otra, se cierra su asignación con la misma fecha y conservación.");

        activo.MapPost("/devolver", DevolverAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("DevolverActivo")
            .WithSummary("Registra la devolución: fecha, conservación, estado en que queda el activo y observaciones.");

        app.MapGet("/asignaciones/movimientos", MovimientosAsync)
            .WithTags("Asignaciones")
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ListarMovimientosAsignacion")
            .WithSummary("Entregas y devoluciones más recientes de la red.");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<AsignacionActivoDto>>, ProblemHttpResult>> ListarAsync(
        Guid id,
        IQueryHandler<ListarAsignacionesActivoQuery, IReadOnlyList<AsignacionActivoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarAsignacionesActivoQuery(id), cancellationToken)).ToHttpResult();

    private static async Task<Results<Created<IdCreado>, ProblemHttpResult>> AsignarAsync(
        Guid id,
        AsignarActivoRequest peticion,
        ICommandHandler<AsignarActivoCommand, IdCreado> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new AsignarActivoCommand(id, peticion.PersonaId, peticion.FechaEntrega, peticion.Conservacion), cancellationToken))
            .ToCreatedResult(_ => $"/api/activos/{id}/asignaciones");

    private static async Task<Results<NoContent, ProblemHttpResult>> DevolverAsync(
        Guid id,
        DevolverActivoRequest peticion,
        ICommandHandler<DevolverActivoCommand, Vacio> handler,
        CancellationToken cancellationToken)
    {
        var comando = new DevolverActivoCommand(id, peticion.Fecha, peticion.Conservacion, peticion.EstadoActivoId, peticion.Observaciones);
        return (await handler.Handle(comando, cancellationToken)).ToNoContentResult();
    }

    private static async Task<Results<Ok<IReadOnlyList<MovimientoDto>>, ProblemHttpResult>> MovimientosAsync(
        Guid? sedeId,
        int? maximo,
        IQueryHandler<ListarMovimientosQuery, IReadOnlyList<MovimientoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarMovimientosQuery(sedeId, maximo), cancellationToken)).ToHttpResult();
}

/// <summary>A quién se entrega, cuándo y en qué estado.</summary>
internal sealed record AsignarActivoRequest(Guid PersonaId, DateOnly FechaEntrega, Conservacion Conservacion);

/// <summary>Datos de la devolución.</summary>
internal sealed record DevolverActivoRequest(DateOnly Fecha, Conservacion Conservacion, Guid EstadoActivoId, string? Observaciones);
