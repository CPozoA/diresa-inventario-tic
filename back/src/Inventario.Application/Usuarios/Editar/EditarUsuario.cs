using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;
using Inventario.Domain.Personal;

namespace Inventario.Application.Usuarios.Editar;

/// <summary>Cambia los datos personales y el correo de un usuario. El nombre de usuario no cambia.</summary>
/// <param name="Dni">
/// Obligatorio si el usuario ya tiene DNI (se puede corregir, no quitar). Los usuarios creados antes de que se pidiera
/// pueden dejarlo vacío hasta completarlo.
/// </param>
/// <remarks>Requiere Usuarios.Gestionar.</remarks>
public sealed record EditarUsuarioCommand(Guid Id, string Nombres, string Apellidos, string? Cargo, string Correo, string? Dni = null)
    : ICommand<Vacio>;

internal sealed class EditarUsuarioHandler(IAppDbContext db, ReglasDeGestion reglas, IServicioIdentidad identidad, IAuditoria auditoria)
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

        var dni = command.Dni?.Trim();
        if (string.IsNullOrEmpty(dni))
        {
            if (usuario.Dni is not null)
            {
                return ErroresUsuario.DniObligatorio;
            }

            dni = null;
        }
        else if (!Persona.EsDniValido(dni))
        {
            return ErroresUsuario.DniInvalido;
        }
        else if (dni != usuario.Dni && await db.BuscarPorDniAsync(dni, usuario.Id, cancellationToken) is not null)
        {
            return ErroresUsuario.DniDuplicado;
        }

        usuario.ActualizarDatos(command.Nombres, command.Apellidos, command.Cargo);
        usuario.CambiarDni(dni);

        auditoria.Registrar(ModuloAuditoria.Usuarios, $"Editó los datos del usuario {usuario.Nombres} {usuario.Apellidos}", usuario.SedeId);

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
