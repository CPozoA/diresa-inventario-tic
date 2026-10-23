using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Sedes.Listar;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

internal static class SedesEndpoints
{
    public static IEndpointRouteBuilder MapSedesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/sedes").WithTags("Sedes");

        grupo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Sedes.Gestionar)
            .WithName("ListarSedes")
            .WithSummary("Lista las sedes ordenadas por nombre.");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<SedeDto>>, ProblemHttpResult>> ListarAsync(
        IQueryHandler<ListarSedesQuery, IReadOnlyList<SedeDto>> handler,
        CancellationToken cancellationToken)
    {
        var resultado = await handler.Handle(new ListarSedesQuery(), cancellationToken);
        return resultado.ToHttpResult();
    }
}
