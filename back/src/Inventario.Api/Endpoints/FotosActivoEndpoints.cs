using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Activos.Fotos;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Endpoints;

/// <summary>
/// Fotos de los activos (D38, docs/ARQUITECTURA.md §5.8). Ver la lista y los archivos: Activos.Ver. Subir, describir,
/// marcar la principal y eliminar: Activos.Editar. Los archivos solo salen por aquí, nunca como archivos públicos.
/// </summary>
internal static class FotosActivoEndpoints
{
    /// <summary>Foto + miniatura + campos del formulario, con margen.</summary>
    private const long TamanoMaximoPeticion = FotoActivo.TamanoMaximoBytes + FotoActivo.TamanoMaximoMiniaturaBytes + (256 * 1024);

    public static IEndpointRouteBuilder MapFotosActivoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/activos/{id:guid}/fotos").WithTags("Fotos de activos");

        grupo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ListarFotosActivo")
            .WithSummary("Fotos del activo: la principal primero y luego en el orden en que se subieron.");

        grupo.MapGet("/{fotoId:guid}/archivo", ArchivoAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ObtenerArchivoFotoActivo")
            .WithSummary("La imagen de la foto (JPEG, PNG o WebP). ?miniatura=true devuelve la versión pequeña para listas y tarjetas.");

        grupo.MapPost("/", SubirAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("SubirFotoActivo")
            .WithSummary("Sube una foto (formulario con archivo, miniatura y descripción). La primera queda como principal.")
            .WithMetadata(new RequestSizeLimitAttribute(TamanoMaximoPeticion))
            // La protección antifalsificación la aplica ValidarCsrfMiddleware (cabecera X-XSRF-TOKEN) a toda la API.
            .DisableAntiforgery();

        grupo.MapPut("/{fotoId:guid}", DescribirAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("DescribirFotoActivo")
            .WithSummary("Cambia la descripción de la foto. Vacía = sin descripción.");

        grupo.MapPost("/{fotoId:guid}/principal", MarcarPrincipalAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("MarcarFotoPrincipalActivo")
            .WithSummary("Hace de esta foto la principal del activo.");

        grupo.MapDelete("/{fotoId:guid}", EliminarAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("EliminarFotoActivo")
            .WithSummary("Elimina la foto y sus archivos. Si era la principal, pasa a serlo la siguiente.");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<FotoActivoDto>>, ProblemHttpResult>> ListarAsync(
        Guid id,
        IQueryHandler<ListarFotosActivoQuery, IReadOnlyList<FotoActivoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarFotosActivoQuery(id), cancellationToken)).ToHttpResult();

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> ArchivoAsync(
        Guid id,
        Guid fotoId,
        bool? miniatura,
        HttpContext contexto,
        IQueryHandler<ObtenerArchivoFotoQuery, ArchivoFotoDto> handler,
        CancellationToken cancellationToken)
    {
        var resultado = await handler.Handle(new ObtenerArchivoFotoQuery(id, fotoId, miniatura ?? false), cancellationToken);
        if (!resultado.EsExito)
        {
            return resultado.Error!.ToProblem();
        }

        // Una foto nunca cambia (otra imagen = otra foto con otro identificador): el navegador puede guardarla mucho
        // tiempo. "private" porque requiere sesión: ningún intermediario debe guardarla.
        contexto.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        contexto.Response.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.Stream(resultado.Valor.Contenido, resultado.Valor.TipoContenido);
    }

    private static async Task<Results<Created<FotoActivoDto>, ProblemHttpResult>> SubirAsync(
        Guid id,
        IFormFile? archivo,
        IFormFile? miniatura,
        [FromForm] string? descripcion,
        ICommandHandler<SubirFotoActivoCommand, FotoActivoDto> handler,
        CancellationToken cancellationToken)
    {
        // Se rechaza antes de leer el archivo a memoria.
        if (archivo is { Length: > FotoActivo.TamanoMaximoBytes } || miniatura is { Length: > FotoActivo.TamanoMaximoMiniaturaBytes })
        {
            return ErroresFoto.MuyGrande.ToProblem();
        }

        var comando = new SubirFotoActivoCommand(
            id,
            await LeerAsync(archivo, cancellationToken),
            await LeerAsync(miniatura, cancellationToken),
            descripcion);
        return (await handler.Handle(comando, cancellationToken)).ToCreatedResult(f => $"/api/activos/{id}/fotos/{f.Id}");
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DescribirAsync(
        Guid id,
        Guid fotoId,
        DescribirFotoRequest datos,
        ICommandHandler<DescribirFotoActivoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new DescribirFotoActivoCommand(id, fotoId, datos.Descripcion), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> MarcarPrincipalAsync(
        Guid id,
        Guid fotoId,
        ICommandHandler<MarcarFotoPrincipalCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new MarcarFotoPrincipalCommand(id, fotoId), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> EliminarAsync(
        Guid id,
        Guid fotoId,
        ICommandHandler<EliminarFotoActivoCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new EliminarFotoActivoCommand(id, fotoId), cancellationToken)).ToNoContentResult();

    private static async Task<ImagenSubida?> LeerAsync(IFormFile? archivo, CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return null;
        }

        using var memoria = new MemoryStream((int)archivo.Length);
        await archivo.CopyToAsync(memoria, cancellationToken);
        return new ImagenSubida(memoria.ToArray());
    }
}

/// <summary>Nueva descripción de una foto.</summary>
/// <param name="Descripcion">Hasta 60 caracteres. Vacía o nula = sin descripción.</param>
internal sealed record DescribirFotoRequest(string? Descripcion);
