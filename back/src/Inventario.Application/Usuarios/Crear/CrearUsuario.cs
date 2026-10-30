using System.Text.RegularExpressions;
using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;
using Inventario.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Usuarios.Crear;

/// <summary>Usuario creado y su contraseña temporal. La contraseña se muestra esta única vez.</summary>
public sealed record UsuarioCreadoDto(Guid Id, string ContrasenaTemporal);

/// <summary>
/// Crea un usuario en una sede con una contraseña temporal que deberá cambiar al ingresar.
/// Para crear un administrador de sede, el superadministrador le asigna el rol Administrador de sede.
/// </summary>
/// <param name="SedeId">Obligatorio para el superadministrador; los demás crean en su sede.</param>
/// <param name="NombreUsuario">Con el que ingresa. Único en todo el sistema.</param>
/// <param name="Correo">Único en todo el sistema. Se usa para recuperar la contraseña.</param>
/// <param name="Nombres">Nombres.</param>
/// <param name="Apellidos">Apellidos.</param>
/// <param name="Cargo">Opcional.</param>
/// <param name="RolIds">Al menos uno. Solo roles cuyos permisos tiene quien crea.</param>
/// <remarks>Requiere Usuarios.Gestionar.</remarks>
public sealed record CrearUsuarioCommand(
    Guid? SedeId,
    string NombreUsuario,
    string Correo,
    string Nombres,
    string Apellidos,
    string? Cargo,
    IReadOnlyList<Guid>? RolIds) : ICommand<UsuarioCreadoDto>;

internal sealed partial class CrearUsuarioHandler(IAppDbContext db, ReglasDeGestion reglas, IServicioIdentidad identidad, IAuditoria auditoria)
    : ICommandHandler<CrearUsuarioCommand, UsuarioCreadoDto>
{
    public async Task<Result<UsuarioCreadoDto>> Handle(CrearUsuarioCommand command, CancellationToken cancellationToken)
    {
        var sede = await reglas.ResolverSedeAsync(command.SedeId, cancellationToken);
        if (!sede.EsExito)
        {
            return sede.Error!;
        }

        var nombreUsuario = command.NombreUsuario?.Trim() ?? string.Empty;
        if (!PatronNombreUsuario().IsMatch(nombreUsuario))
        {
            return ErroresUsuario.NombreUsuarioNoValido;
        }

        var correo = command.Correo?.Trim();
        if (string.IsNullOrEmpty(correo))
        {
            return ErroresUsuario.CorreoObligatorio;
        }

        if (await identidad.ExisteNombreUsuarioAsync(nombreUsuario))
        {
            return ErroresUsuario.NombreUsuarioDuplicado;
        }

        if (await identidad.ExisteCorreoAsync(correo, excluirUsuarioId: null))
        {
            return ErroresUsuario.CorreoDuplicado;
        }

        var rolIds = (command.RolIds ?? []).Distinct().ToList();
        var roles = await db.Roles
            .Include(r => r.PermisosAsignados)
            .Where(r => rolIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (roles.Count != rolIds.Count)
        {
            return ErroresUsuario.RolNoEncontrado;
        }

        var noPuede = await reglas.VerificarPuedeAsignarRolesAsync(roles, cancellationToken);
        if (noPuede is not null)
        {
            return noPuede;
        }

        // El dominio valida nombres y longitudes, que haya al menos un rol y que los roles sean de la misma sede.
        var usuario = Usuario.CrearDeSede(sede.Valor, command.Nombres, command.Apellidos, command.Cargo);
        usuario.AsignarRoles(roles);
        db.Usuarios.Add(usuario);

        var contrasenaTemporal = GeneradorContrasenaTemporal.Generar();

        // Guarda usuario, roles y cuenta en una sola operación.
        var cuenta = await identidad.CrearCuentaAsync(usuario.Id, nombreUsuario, correo, contrasenaTemporal);
        if (!cuenta.EsExito)
        {
            db.Usuarios.Remove(usuario);
            return cuenta.Error!;
        }

        await auditoria.RegistrarYGuardarAsync(
            ModuloAuditoria.Usuarios, $"Creó el usuario {nombreUsuario} ({usuario.Nombres} {usuario.Apellidos})", usuario.SedeId, cancellationToken: cancellationToken);

        return new UsuarioCreadoDto(usuario.Id, contrasenaTemporal);
    }

    [GeneratedRegex("^[A-Za-z0-9._@-]{3,50}$")]
    private static partial Regex PatronNombreUsuario();
}
