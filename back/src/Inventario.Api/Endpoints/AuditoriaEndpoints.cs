using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Activos.Exportar;
using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Domain.Auditoria;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>
/// Registro de auditoría (D56, docs/ARQUITECTURA.md §5.16): quién hizo qué y cuándo. Requiere Auditoria.Ver. Un
/// administrador de sede ve lo de su red; el superadministrador, todo. Solo lectura: no se modifica ni se borra.
/// </summary>
internal static class AuditoriaEndpoints
{
    private const string TipoExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static IEndpointRouteBuilder MapAuditoriaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/auditoria").WithTags("Auditoría");

        grupo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Auditoria.Ver)
            .WithName("ListarAuditoria")
            .WithSummary("Acciones registradas, lo más reciente primero. ?desde=&hasta= (días, hora local), ?modulo=, ?buscar= en la acción, el usuario o la IP.");

        grupo.MapGet("/exportar", ExportarAsync)
            .RequirePermiso(Permisos.Auditoria.Ver)
            .WithName("ExportarAuditoria")
            .WithSummary("El registro en Excel (.xlsx) con los mismos filtros. Hasta 50 000 acciones.")
            .Produces(StatusCodes.Status200OK, contentType: TipoExcel);

        app.MapGet("/activos/{id:guid}/historial", HistorialAsync)
            .WithTags("Auditoría")
            .RequirePermiso(Permisos.Auditoria.Ver)
            .WithName("ObtenerHistorialActivo")
            .WithSummary("Acciones sobre el activo (registro, ediciones, fotos, asignaciones, mantenimientos, licencias), lo más reciente primero.");

        return app;
    }

    private static async Task<Results<Ok<PaginaAuditoriaDto>, ProblemHttpResult>> ListarAsync(
        Guid? sedeId,
        DateOnly? desde,
        DateOnly? hasta,
        ModuloAuditoria? modulo,
        string? buscar,
        int? pagina,
        int? tamanoPagina,
        IQueryHandler<ListarAuditoriaQuery, PaginaAuditoriaDto> handler,
        CancellationToken cancellationToken)
    {
        var consulta = new ListarAuditoriaQuery(new FiltrosAuditoria(sedeId, desde, hasta, modulo, buscar), pagina, tamanoPagina);
        return (await handler.Handle(consulta, cancellationToken)).ToHttpResult();
    }

    private static async Task<Results<FileContentHttpResult, ProblemHttpResult>> ExportarAsync(
        Guid? sedeId,
        DateOnly? desde,
        DateOnly? hasta,
        ModuloAuditoria? modulo,
        string? buscar,
        IQueryHandler<ExportarAuditoriaQuery, ArchivoDescargable> handler,
        CancellationToken cancellationToken)
    {
        var resultado = await handler.Handle(new ExportarAuditoriaQuery(new FiltrosAuditoria(sedeId, desde, hasta, modulo, buscar)), cancellationToken);
        return resultado.EsExito
            ? TypedResults.File(resultado.Valor!.Contenido, TipoExcel, resultado.Valor.NombreArchivo)
            : resultado.Error!.ToProblem();
    }

    private static async Task<Results<Ok<IReadOnlyList<RegistroAuditoriaDto>>, ProblemHttpResult>> HistorialAsync(
        Guid id,
        IQueryHandler<ObtenerHistorialActivoQuery, IReadOnlyList<RegistroAuditoriaDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerHistorialActivoQuery(id), cancellationToken)).ToHttpResult();
}
