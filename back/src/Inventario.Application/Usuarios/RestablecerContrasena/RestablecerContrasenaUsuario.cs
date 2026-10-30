using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;

namespace Inventario.Application.Usuarios.RestablecerContrasena;

/// <summary>Contraseña temporal nueva. Se muestra esta única vez.</summary>
public sealed record ContrasenaTemporalDto(string ContrasenaTemporal);

/// <summary>
/// El administrador restablece la contraseña de un usuario: genera una temporal, quita el bloqueo,
/// cierra sus sesiones y le exige cambiarla al ingresar.
/// </summary>
/// <remarks>Requiere Usuarios.Gestionar.</remarks>
public sealed record RestablecerContrasenaUsuarioCommand(Guid Id) : ICommand<ContrasenaTemporalDto>;

internal sealed class RestablecerContrasenaUsuarioHandler(IAppDbContext db, ReglasDeGestion reglas, IServicioIdentidad identidad, IAuditoria auditoria)
    : ICommandHandler<RestablecerContrasenaUsuarioCommand, ContrasenaTemporalDto>
{
    public async Task<Result<ContrasenaTemporalDto>> Handle(
        RestablecerContrasenaUsuarioCommand command,
        CancellationToken cancellationToken)
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

        var contrasenaTemporal = GeneradorContrasenaTemporal.Generar();

        var resultado = await identidad.EstablecerContrasenaTemporalAsync(usuario.Id, contrasenaTemporal);
        if (resultado.EsExito)
        {
            await auditoria.RegistrarYGuardarAsync(
                ModuloAuditoria.Usuarios, $"Restableció la contraseña de {usuario.Nombres} {usuario.Apellidos}", usuario.SedeId, cancellationToken: cancellationToken);
        }

        return resultado.EsExito ? new ContrasenaTemporalDto(contrasenaTemporal) : resultado.Error!;
    }
}
