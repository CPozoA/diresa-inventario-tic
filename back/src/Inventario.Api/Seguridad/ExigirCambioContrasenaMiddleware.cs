using Inventario.Application.Common.Seguridad;

namespace Inventario.Api.Seguridad;

/// <summary>
/// Mientras el usuario tenga una contraseña temporal, solo puede usar los endpoints de su cuenta
/// (ver su sesión, cambiar la contraseña, cerrar sesión). Cualquier otra petición recibe 403.
/// </summary>
internal sealed class ExigirCambioContrasenaMiddleware(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        var debeCambiar = contexto.User.HasClaim(TiposClaim.DebeCambiarContrasena, bool.TrueString);
        var ruta = contexto.Request.Path;

        if (debeCambiar && ruta.StartsWithSegments("/api") && !ruta.StartsWithSegments("/api/cuenta"))
        {
            await TypedResults.Problem(
                    title: "Debe cambiar su contraseña temporal antes de continuar.",
                    statusCode: StatusCodes.Status403Forbidden,
                    extensions: new Dictionary<string, object?> { ["codigo"] = "Cuenta.DebeCambiarContrasena" })
                .ExecuteAsync(contexto);
            return;
        }

        await siguiente(contexto);
    }
}
