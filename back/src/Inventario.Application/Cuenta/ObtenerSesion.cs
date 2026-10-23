using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;

namespace Inventario.Application.Cuenta;

/// <summary>Datos del usuario conectado. El frontend lo consulta al cargar para saber si hay sesión.</summary>
public sealed record ObtenerSesionQuery : IQuery<SesionDto>;

internal sealed class ObtenerSesionHandler(IUsuarioActual usuarioActual, ConstructorSesion constructorSesion)
    : IQueryHandler<ObtenerSesionQuery, SesionDto>
{
    public async Task<Result<SesionDto>> Handle(ObtenerSesionQuery query, CancellationToken cancellationToken)
    {
        if (usuarioActual.UsuarioId is not Guid usuarioId)
        {
            return ErroresCuenta.SinSesion;
        }

        var sesion = await constructorSesion.ConstruirAsync(usuarioId, cancellationToken);
        return sesion is null
            ? ErroresCuenta.SinSesion
            : Result.Exito(sesion);
    }
}
