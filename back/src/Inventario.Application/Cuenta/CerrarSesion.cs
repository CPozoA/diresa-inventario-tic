using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;

namespace Inventario.Application.Cuenta;

public sealed record CerrarSesionCommand : ICommand<Vacio>;

internal sealed class CerrarSesionHandler(IServicioIdentidad identidad) : ICommandHandler<CerrarSesionCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CerrarSesionCommand command, CancellationToken cancellationToken)
    {
        await identidad.CerrarSesionAsync();
        return Result.Exito(Vacio.Valor);
    }
}
