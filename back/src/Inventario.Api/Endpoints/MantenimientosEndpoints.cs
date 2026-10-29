using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Mantenimientos;
using Inventario.Domain.Mantenimientos;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>
/// Mantenimiento de los activos (D53, docs/ARQUITECTURA.md §5.13). Consultar: Mantenimiento.Ver. Registrar y cerrar:
/// Mantenimiento.Gestionar. Cada usuario de sede solo ve y toca los de su red.
/// </summary>
internal static class MantenimientosEndpoints
{
    public static IEndpointRouteBuilder MapMantenimientosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/mantenimientos").WithTags("Mantenimiento");

        grupo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Mantenimiento.Ver)
            .WithName("ListarMantenimientos")
            .WithSummary("Mantenimientos en curso (el más antiguo primero) o el historial (el más reciente primero), paginados. ?buscar= código, modelo, marca, proveedor, orden de servicio o técnico.");

        grupo.MapGet("/programados", ProgramadosAsync)
            .RequirePermiso(Permisos.Mantenimiento.Ver)
            .WithName("ListarMantenimientosProgramados")
            .WithSummary("Activos con el preventivo vencido o en los próximos ?dias= (por omisión 30), el más atrasado primero. Sin los de baja ni los que tienen uno en curso.");

        grupo.MapGet("/cifras", CifrasAsync)
            .RequirePermiso(Permisos.Mantenimiento.Ver)
            .WithName("ObtenerCifrasMantenimiento")
            .WithSummary("En curso, preventivos vencidos y preventivos en los próximos ?dias=.");

        grupo.MapGet("/tecnicos", TecnicosAsync)
            .RequirePermiso(Permisos.Mantenimiento.Gestionar)
            .WithName("ListarTecnicosMantenimiento")
            .WithSummary("Usuarios activos de la red con permiso para gestionar mantenimientos. El superadministrador indica ?sedeId=.");

        grupo.MapPost("/{id:guid}/cerrar", CerrarAsync)
            .RequirePermiso(Permisos.Mantenimiento.Gestionar)
            .WithName("CerrarMantenimiento")
            .WithSummary("Cierra el mantenimiento: trabajo realizado, resultado, estado y conservación en que queda el activo y próximo preventivo.");

        var activo = app.MapGroup("/activos/{id:guid}/mantenimientos").WithTags("Mantenimiento");

        activo.MapGet("/", DelActivoAsync)
            .RequirePermiso(Permisos.Mantenimiento.Ver)
            .WithName("ObtenerMantenimientosActivo")
            .WithSummary("Mantenimiento del activo: el en curso, el último y el próximo preventivo, y el historial.");

        activo.MapPost("/", RegistrarAsync)
            .RequirePermiso(Permisos.Mantenimiento.Gestionar)
            .WithName("RegistrarMantenimiento")
            .WithSummary("Registra un mantenimiento; abierto, el activo pasa a 'En mantenimiento'. Con 'cierre' se registra ya terminado.");

        return app;
    }

    private static async Task<Results<Ok<PaginaMantenimientosDto>, ProblemHttpResult>> ListarAsync(
        VistaMantenimientos? vista,
        Guid? sedeId,
        string? buscar,
        TipoMantenimiento? tipo,
        Guid? establecimientoId,
        int? pagina,
        int? tamanoPagina,
        IQueryHandler<ListarMantenimientosQuery, PaginaMantenimientosDto> handler,
        CancellationToken cancellationToken)
    {
        var consulta = new ListarMantenimientosQuery(
            vista ?? VistaMantenimientos.EnCurso, sedeId, buscar, tipo, establecimientoId, pagina, tamanoPagina);
        return (await handler.Handle(consulta, cancellationToken)).ToHttpResult();
    }

    private static async Task<Results<Ok<IReadOnlyList<ProgramadoDto>>, ProblemHttpResult>> ProgramadosAsync(
        Guid? sedeId,
        Guid? establecimientoId,
        string? buscar,
        int? dias,
        IQueryHandler<ListarProgramadosQuery, IReadOnlyList<ProgramadoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarProgramadosQuery(sedeId, establecimientoId, buscar, dias), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<CifrasMantenimientoDto>, ProblemHttpResult>> CifrasAsync(
        Guid? sedeId,
        int? dias,
        IQueryHandler<ObtenerCifrasMantenimientoQuery, CifrasMantenimientoDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerCifrasMantenimientoQuery(sedeId, dias), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<TecnicoDto>>, ProblemHttpResult>> TecnicosAsync(
        Guid? sedeId,
        IQueryHandler<ListarTecnicosQuery, IReadOnlyList<TecnicoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarTecnicosQuery(sedeId), cancellationToken)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> CerrarAsync(
        Guid id,
        CierreMantenimiento cierre,
        ICommandHandler<CerrarMantenimientoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CerrarMantenimientoCommand(id, cierre), cancellationToken)).ToNoContentResult();

    private static async Task<Results<Ok<MantenimientosActivoDto>, ProblemHttpResult>> DelActivoAsync(
        Guid id,
        IQueryHandler<ObtenerMantenimientosActivoQuery, MantenimientosActivoDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerMantenimientosActivoQuery(id), cancellationToken)).ToHttpResult();

    private static async Task<Results<Created<IdCreado>, ProblemHttpResult>> RegistrarAsync(
        Guid id,
        RegistrarMantenimientoRequest peticion,
        ICommandHandler<RegistrarMantenimientoCommand, IdCreado> handler,
        CancellationToken cancellationToken)
    {
        var comando = new RegistrarMantenimientoCommand(
            id,
            peticion.Tipo,
            peticion.FechaInicio,
            peticion.Detalle,
            new DatosEjecutor(peticion.Ejecutor, peticion.TecnicoId, peticion.Proveedor, peticion.OrdenServicio, peticion.Costo),
            peticion.Cierre);
        return (await handler.Handle(comando, cancellationToken)).ToCreatedResult(_ => $"/api/activos/{id}/mantenimientos");
    }
}

/// <summary>Datos para registrar un mantenimiento. Con <c>cierre</c>, se registra ya terminado.</summary>
internal sealed record RegistrarMantenimientoRequest(
    TipoMantenimiento Tipo,
    DateOnly FechaInicio,
    string? Detalle,
    EjecutorMantenimiento Ejecutor,
    Guid? TecnicoId,
    string? Proveedor,
    string? OrdenServicio,
    decimal? Costo,
    CierreMantenimiento? Cierre);
