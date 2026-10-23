using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;

namespace Inventario.Application.Cuenta;

/// <summary>El usuario conectado cambia su propia contraseña. Obligatorio tras recibir una contraseña temporal.</summary>
public sealed record CambiarContrasenaCommand(string ContrasenaActual, string ContrasenaNueva) : ICommand<Vacio>;

internal sealed class CambiarContrasenaHandler(IUsuarioActual usuarioActual, IServicioIdentidad identidad)
    : ICommandHandler<CambiarContrasenaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CambiarContrasenaCommand command, CancellationToken cancellationToken)
    {
        if (usuarioActual.UsuarioId is not Guid usuarioId)
        {
            return ErroresCuenta.SinSesion;
        }

        if (string.IsNullOrEmpty(command.ContrasenaActual) || string.IsNullOrEmpty(command.ContrasenaNueva))
        {
            return ErroresCuenta.DatosIncompletos;
        }

        if (string.Equals(command.ContrasenaActual, command.ContrasenaNueva, StringComparison.Ordinal))
        {
            return ErroresCuenta.ContrasenaIgual;
        }

        var resultado = await identidad.CambiarContrasenaAsync(usuarioId, command.ContrasenaActual, command.ContrasenaNueva);
        return resultado.EsExito
            ? Result.Exito(Vacio.Valor)
            : Result.Fallo<Vacio>(resultado.Error!);
    }
}
