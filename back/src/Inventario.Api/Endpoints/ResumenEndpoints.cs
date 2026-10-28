using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Resumen;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>Resumen del inicio (docs/ARQUITECTURA.md §5.11). Requiere Activos.Ver.</summary>
internal static class ResumenEndpoints
{
    public static IEndpointRouteBuilder MapResumenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/resumen").WithTags("Resumen")
            .MapGet("/", ObtenerAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ObtenerResumen")
            .WithSummary("Cifras del inicio: activos, estados, antigüedad, tipos, establecimientos, atención y fichas. El superadministrador ve todas las redes (con la tabla por red) o ?sedeId=.");

        return app;
    }

    private static async Task<Results<Ok<ResumenDto>, ProblemHttpResult>> ObtenerAsync(
        Guid? sedeId,
        IQueryHandler<ObtenerResumenQuery, ResumenDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerResumenQuery(sedeId), cancellationToken)).ToHttpResult();
}
