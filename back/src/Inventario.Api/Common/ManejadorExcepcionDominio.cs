using Inventario.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Common;

/// <summary>
/// Convierte una <see cref="DominioException"/> (dato que incumple una regla del dominio: un nombre vacío,
/// un código con caracteres no permitidos, un rol de otra sede…) en HTTP 400 con su mensaje, que ya está
/// escrito para el usuario. Cualquier otra excepción sigue al manejador genérico (500 sin detalles).
/// </summary>
internal sealed class ManejadorExcepcionDominio(IProblemDetailsService problemas) : IExceptionHandler
{
    public const string Codigo = "Dominio.ReglaIncumplida";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not DominioException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = exception.Message,
                Extensions = { ["codigo"] = Codigo },
            },
        });
    }
}
