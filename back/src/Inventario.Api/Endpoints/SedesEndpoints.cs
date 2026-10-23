using Inventario.Api.Common;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Sedes.Listar;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

internal static class SedesEndpoints
{
    public static IEndpointRouteBuilder MapSedesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/sedes").WithTags("Sedes");

        // Exige sesión iniciada (heredado del grupo /api). En el Paso 4 se exige además el permiso Sedes.Gestionar.
        grupo.MapGet("/", ListarAsync)
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
