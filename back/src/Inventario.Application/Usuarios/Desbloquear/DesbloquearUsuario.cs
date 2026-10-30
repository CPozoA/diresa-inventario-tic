using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;

namespace Inventario.Application.Usuarios.Desbloquear;

/// <summary>Quita el bloqueo por intentos fallidos antes de que pasen los 15 minutos.</summary>
/// <remarks>Requiere Usuarios.Gestionar.</remarks>
public sealed record DesbloquearUsuarioCommand(Guid Id) : ICommand<Vacio>;

internal sealed class DesbloquearUsuarioHandler(IAppDbContext db, ReglasDeGestion reglas, IServicioIdentidad identidad, IAuditoria auditoria)
    : ICommandHandler<DesbloquearUsuarioCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(DesbloquearUsuarioCommand command, CancellationToken cancellationToken)
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

        await identidad.DesbloquearAsync(usuario.Id);
        await auditoria.RegistrarYGuardarAsync(
            ModuloAuditoria.Usuarios, $"Desbloqueó la cuenta de {usuario.Nombres} {usuario.Apellidos}", usuario.SedeId, cancellationToken: cancellationToken);
        return Vacio.Valor;
    }
}
