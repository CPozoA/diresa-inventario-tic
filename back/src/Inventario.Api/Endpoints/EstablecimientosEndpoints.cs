using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Establecimientos.Gestionar;
using Inventario.Application.Establecimientos.Importar;
using Inventario.Application.Establecimientos.Listar;
using Inventario.Domain.Establecimientos;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>
/// Establecimientos de salud (docs/ARQUITECTURA.md §5.9). Consultar: cualquier usuario con sesión (un usuario de sede
/// solo ve los de su red). Registrar, corregir, desactivar e importar: Establecimientos.Gestionar (solo el superadministrador).
/// </summary>
internal static class EstablecimientosEndpoints
{
    private const string TipoExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static IEndpointRouteBuilder MapEstablecimientosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/establecimientos").WithTags("Establecimientos");

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarEstablecimientos")
            .WithSummary("Establecimientos ordenados por nombre. ?buscar= código único, Id_Establecimiento o parte del nombre.");

        const string gestionar = Permisos.Establecimientos.Gestionar;

        grupo.MapPost("/", CrearAsync)
            .RequirePermiso(gestionar)
            .WithName("CrearEstablecimiento")
            .WithSummary("Registra un establecimiento en una red.");

        grupo.MapPut("/{id:guid}", EditarAsync)
            .RequirePermiso(gestionar)
            .WithName("EditarEstablecimiento")
            .WithSummary("Corrige los datos de un establecimiento. La red no cambia.");

        grupo.MapPost("/{id:guid}/activar", ActivarAsync)
            .RequirePermiso(gestionar)
            .WithName("ActivarEstablecimiento")
            .WithSummary("Vuelve a ofrecer el establecimiento al registrar activos.");

        grupo.MapPost("/{id:guid}/desactivar", DesactivarAsync)
            .RequirePermiso(gestionar)
            .WithName("DesactivarEstablecimiento")
            .WithSummary("Deja de ofrecer el establecimiento al registrar activos; se conserva en los que ya lo usan.");

        grupo.MapGet("/plantilla", Plantilla)
            .RequirePermiso(gestionar)
            .WithName("DescargarPlantillaEstablecimientos")
            .WithSummary("Plantilla de Excel para importar establecimientos.");

        grupo.MapPost("/importar", ImportarAsync)
            .RequirePermiso(gestionar)
            .WithName("ImportarEstablecimientos")
            .WithSummary("Importa establecimientos desde Excel. Si alguna fila tiene errores, no se guarda nada.")
            // La protección antifalsificación la aplica ValidarCsrfMiddleware (cabecera X-XSRF-TOKEN) a toda la API.
            // Sin esto, ASP.NET Core exigiría además su propio middleware para los formularios con archivos.
            .DisableAntiforgery();

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<EstablecimientoDto>>, ProblemHttpResult>> ListarAsync(
        Guid? sedeId,
        string? buscar,
        bool? incluirInactivos,
        int? maximo,
        IQueryHandler<ListarEstablecimientosQuery, IReadOnlyList<EstablecimientoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(
            new ListarEstablecimientosQuery(sedeId, buscar, incluirInactivos ?? false, maximo),
            cancellationToken)).ToHttpResult();

    private static async Task<Results<Created<IdCreado>, ProblemHttpResult>> CrearAsync(
        CrearEstablecimientoCommand comando,
        ICommandHandler<CrearEstablecimientoCommand, IdCreado> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(comando, cancellationToken)).ToCreatedResult(c => $"/api/establecimientos/{c.Id}");

    private static async Task<Results<NoContent, ProblemHttpResult>> EditarAsync(
        Guid id,
        EditarEstablecimientoRequest cuerpo,
        ICommandHandler<EditarEstablecimientoCommand, Vacio> handler,
        CancellationToken cancellationToken)
    {
        var datos = new DatosEstablecimiento(
            cuerpo.IdEstablecimiento,
            cuerpo.CodigoUnico,
            cuerpo.Nombre,
            cuerpo.Categoria,
            cuerpo.Microred,
            cuerpo.Provincia,
            cuerpo.Distrito,
            cuerpo.Ubigeo);

        return (await handler.Handle(new EditarEstablecimientoCommand(id, datos), cancellationToken)).ToNoContentResult();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ActivarAsync(
        Guid id,
        ICommandHandler<CambiarEstadoEstablecimientoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CambiarEstadoEstablecimientoCommand(id, Activo: true), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DesactivarAsync(
        Guid id,
        ICommandHandler<CambiarEstadoEstablecimientoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CambiarEstadoEstablecimientoCommand(id, Activo: false), cancellationToken)).ToNoContentResult();

    private static FileContentHttpResult Plantilla(IHojaEstablecimientos hoja) =>
        TypedResults.File(hoja.CrearPlantilla(), TipoExcel, "plantilla-establecimientos.xlsx");

    private static async Task<Results<Ok<ResultadoImportacionDto>, ProblemHttpResult>> ImportarAsync(
        IFormFile? archivo,
        ICommandHandler<ImportarEstablecimientosCommand, ResultadoImportacionDto> handler,
        CancellationToken cancellationToken)
    {
        await using var contenido = archivo?.OpenReadStream();
        var comando = new ImportarEstablecimientosCommand(contenido, archivo?.Length ?? 0);
        return (await handler.Handle(comando, cancellationToken)).ToHttpResult();
    }
}

internal sealed record EditarEstablecimientoRequest(
    int IdEstablecimiento,
    int CodigoUnico,
    string Nombre,
    string? Categoria,
    string? Microred,
    string? Provincia,
    string? Distrito,
    string? Ubigeo);
