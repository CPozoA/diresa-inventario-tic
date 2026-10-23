using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;

namespace Inventario.Application.Usuarios.Editar;

/// <summary>Cambia los datos personales y el correo de un usuario. El nombre de usuario no cambia.</summary>
/// <remarks>Requiere Usuarios.Gestionar.</remarks>
public sealed record EditarUsuarioCommand(Guid Id, string Nombres, string Apellidos, string? Cargo, string Correo)
    : ICommand<Vacio>;

internal sealed class EditarUsuarioHandler(IAppDbContext db, ReglasDeGestion reglas, IServicioIdentidad identidad)
    : ICommandHandler<EditarUsuarioCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EditarUsuarioCommand command, CancellationToken cancellationToken)
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

        var correo = command.Correo?.Trim();
        if (string.IsNullOrEmpty(correo))
        {
            return ErroresUsuario.CorreoObligatorio;
        }

        usuario.ActualizarDatos(command.Nombres, command.Apellidos, command.Cargo);

        var cuenta = await identidad.ObtenerDatosCuentaAsync(usuario.Id);
        var cambiaCorreo = !string.Equals(cuenta?.Correo, correo, StringComparison.OrdinalIgnoreCase);

        if (!cambiaCorreo)
        {
            await db.SaveChangesAsync(cancellationToken);
            return Vacio.Valor;
        }

        if (await identidad.ExisteCorreoAsync(correo, excluirUsuarioId: usuario.Id))
        {
            return ErroresUsuario.CorreoDuplicado;
        }

        // Guarda el correo y los datos del usuario en una sola operación.
        var resultado = await identidad.CambiarCorreoAsync(usuario.Id, correo);
        return resultado.EsExito ? Vacio.Valor : resultado.Error!;
    }
}
