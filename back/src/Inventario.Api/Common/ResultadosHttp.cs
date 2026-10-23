using Inventario.Application.Common.Resultados;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Common;

/// <summary>
/// Traduce el resultado de un caso de uso a una respuesta HTTP.
/// Los errores esperados salen como ProblemDetails (RFC 9457) con un campo <c>codigo</c> estable para el frontend.
/// </summary>
internal static class ResultadosHttp
{
    public static Results<Ok<T>, ProblemHttpResult> ToHttpResult<T>(this Result<T> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.EsExito
            ? TypedResults.Ok(resultado.Valor)
            : ToProblem(resultado.Error!);
    }

    /// <summary>Para comandos sin valor de retorno: éxito = 204 No Content.</summary>
    public static Results<NoContent, ProblemHttpResult> ToNoContentResult(this Result<Vacio> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        return resultado.EsExito
            ? TypedResults.NoContent()
            : ToProblem(resultado.Error!);
    }

    /// <summary>Para comandos que crean un registro: éxito = 201 Created con la ubicación del nuevo recurso.</summary>
    public static Results<Created<T>, ProblemHttpResult> ToCreatedResult<T>(this Result<T> resultado, Func<T, string> ubicacion)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(ubicacion);

        return resultado.EsExito
            ? TypedResults.Created(ubicacion(resultado.Valor), resultado.Valor)
            : ToProblem(resultado.Error!);
    }

    public static ProblemHttpResult ToProblem(this ErrorAplicacion error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var estado = error.Tipo switch
        {
            TipoError.Validacion => StatusCodes.Status400BadRequest,
            TipoError.NoAutenticado => StatusCodes.Status401Unauthorized,
            TipoError.NoEncontrado => StatusCodes.Status404NotFound,
            TipoError.Conflicto => StatusCodes.Status409Conflict,
            TipoError.Prohibido => StatusCodes.Status403Forbidden,
            TipoError.ReglaDeNegocio => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError,
        };

        return TypedResults.Problem(
            title: error.Mensaje,
            statusCode: estado,
            extensions: new Dictionary<string, object?> { ["codigo"] = error.Codigo });
    }
}
