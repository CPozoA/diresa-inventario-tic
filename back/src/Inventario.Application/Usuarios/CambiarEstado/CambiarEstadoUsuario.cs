using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;

namespace Inventario.Application.Usuarios.CambiarEstado;

/// <summary>
/// Activa o desactiva un usuario. Un usuario inactivo conserva su historial, pierde sus permisos al instante
/// y sus sesiones abiertas se cierran.
/// </summary>
/// <remarks>Requiere Usuarios.Gestionar.</remarks>
public sealed record CambiarEstadoUsuarioCommand(Guid Id, bool Activo) : ICommand<Vacio>;

internal sealed class CambiarEstadoUsuarioHandler(IAppDbContext db, ReglasDeGestion reglas, IServicioIdentidad identidad)
    : ICommandHandler<CambiarEstadoUsuarioCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CambiarEstadoUsuarioCommand command, CancellationToken cancellationToken)
    {
        var usuario = await db.BuscarParaGestionAsync(command.Id, cancellationToken);
        if (usuario is null)
        {
            return ErroresUsuario.NoEncontrado;
        }

        var noPuede = await reglas.VerificarPuedeGestionarAsync(usuario, cancellationToken);
        if (noPuede is not null)
        {
            return noPuede;
        }

        if (command.Activo)
        {
            usuario.Activar();
            await db.SaveChangesAsync(cancellationToken);
            return Vacio.Valor;
        }

        usuario.Desactivar();
        await db.SaveChangesAsync(cancellationToken);
        await identidad.InvalidarSesionesAsync(usuario.Id);

        return Vacio.Valor;
    }
}
