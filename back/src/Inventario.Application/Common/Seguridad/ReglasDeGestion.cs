using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Seguridad;
using Inventario.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Common.Seguridad;

/// <summary>Errores de las reglas de gestión de usuarios, roles y sedes.</summary>
public static class ErroresGestion
{
    public static readonly ErrorAplicacion SinSede =
        ErrorAplicacion.Prohibido("Gestion.SinSede", "Su cuenta no pertenece a ninguna sede.");

    public static readonly ErrorAplicacion OtraSede =
        ErrorAplicacion.Prohibido("Gestion.OtraSede", "Solo puede gestionar datos de su propia sede.");

    public static readonly ErrorAplicacion SedeObligatoria =
        ErrorAplicacion.Validacion("Gestion.SedeObligatoria", "Indique la sede.");

    public static readonly ErrorAplicacion SedeNoEncontrada =
        ErrorAplicacion.NoEncontrado("Gestion.SedeNoEncontrada", "La sede no existe.");

    public static readonly ErrorAplicacion SoloSuperAdminGestionaAdministradores =
        ErrorAplicacion.Prohibido(
            "Gestion.SoloSuperAdminGestionaAdministradores",
            "Solo el superadministrador puede asignar, quitar o gestionar el rol Administrador de sede.");

    public static readonly ErrorAplicacion NoASiMismo =
        ErrorAplicacion.Prohibido(
            "Gestion.NoASiMismo",
            "No puede modificar sus propios roles, estado ni contraseña desde aquí. Use las opciones de su cuenta.");

    public static readonly ErrorAplicacion NoAlSuperAdmin =
        ErrorAplicacion.Prohibido("Gestion.NoAlSuperAdmin", "La cuenta del superadministrador no se gestiona desde aquí.");

    public static readonly ErrorAplicacion ObjetivoConMasPermisos =
        ErrorAplicacion.Prohibido(
            "Gestion.ObjetivoConMasPermisos",
            "No puede gestionar a un usuario que tiene permisos que usted no tiene.");

    public static ErrorAplicacion PermisosQueNoTiene(IEnumerable<string> permisos) =>
        ErrorAplicacion.Prohibido(
            "Gestion.PermisosQueNoTiene",
            $"No puede otorgar permisos que usted no tiene: {string.Join(", ", permisos)}.");
}

/// <summary>
/// Reglas contra el escalamiento de privilegios, que dependen de QUIÉN hace la acción (docs/ARQUITECTURA.md §6.3).
/// Las reglas que solo dependen del usuario y sus roles están en el dominio (<see cref="Usuario.AsignarRoles"/>).
/// </summary>
internal sealed class ReglasDeGestion(
    IUsuarioActual actual,
    IPermisosUsuarioActual permisosActuales,
    CalculadoraPermisos calculadora,
    IAppDbContext db)
{
    /// <summary>
    /// Sede sobre la que se opera. Un usuario de sede solo opera sobre la suya (si indica otra, se rechaza);
    /// el superadministrador debe indicarla.
    /// </summary>
    public async Task<Result<Guid>> ResolverSedeAsync(Guid? sedeSolicitada, CancellationToken cancellationToken)
    {
        if (!actual.EsSuperAdmin)
        {
            if (actual.SedeId is not Guid propia)
            {
                return ErroresGestion.SinSede;
            }

            if (sedeSolicitada is Guid otra && otra != propia)
            {
                return ErroresGestion.OtraSede;
            }

            return Result.Exito(propia);
        }

        if (sedeSolicitada is not Guid sede)
        {
            return ErroresGestion.SedeObligatoria;
        }

        return await db.Sedes.AnyAsync(s => s.Id == sede, cancellationToken)
            ? Result.Exito(sede)
            : ErroresGestion.SedeNoEncontrada;
    }

    /// <summary>Solo se otorgan permisos que quien actúa ya tiene. El superadministrador puede todos.</summary>
    public async Task<ErrorAplicacion?> VerificarPuedeOtorgarPermisosAsync(
        IEnumerable<string> permisos,
        CancellationToken cancellationToken)
    {
        if (actual.EsSuperAdmin)
        {
            return null;
        }

        var propios = await permisosActuales.ObtenerAsync(cancellationToken);
        var faltantes = permisos.Where(p => !propios.Contains(p)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        return faltantes.Count == 0 ? null : ErroresGestion.PermisosQueNoTiene(faltantes);
    }

    /// <summary>
    /// Para asignar roles: el rol Administrador de sede solo lo asigna el superadministrador,
    /// y nadie otorga (a través de los roles) permisos que no tiene.
    /// </summary>
    public async Task<ErrorAplicacion?> VerificarPuedeAsignarRolesAsync(
        IReadOnlyCollection<Rol> roles,
        CancellationToken cancellationToken)
    {
        if (actual.EsSuperAdmin)
        {
            return null;
        }

        if (roles.Any(r => r.Tipo == TipoRol.AdministradorSede))
        {
            return ErroresGestion.SoloSuperAdminGestionaAdministradores;
        }

        return await VerificarPuedeOtorgarPermisosAsync(roles.SelectMany(r => r.PermisosEfectivos()), cancellationToken);
    }

    /// <summary>
    /// Para gestionar a otro usuario (editar, roles, activar/desactivar, contraseña, desbloqueo):
    /// nunca a sí mismo ni al superadministrador; a un administrador de sede solo lo gestiona el superadministrador;
    /// y nadie gestiona a quien tiene permisos que él no tiene (evita, por ejemplo, restablecer la contraseña
    /// de una cuenta con más privilegios y apropiarse de ella).
    /// </summary>
    public async Task<ErrorAplicacion?> VerificarPuedeGestionarAsync(Usuario objetivo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objetivo);

        if (objetivo.Id == actual.UsuarioId)
        {
            return ErroresGestion.NoASiMismo;
        }

        if (objetivo.EsSuperAdmin)
        {
            return ErroresGestion.NoAlSuperAdmin;
        }

        if (actual.EsSuperAdmin)
        {
            return null;
        }

        if (objetivo.Roles.Any(r => r.RolId == RolesDelSistema.AdministradorSedeId))
        {
            return ErroresGestion.SoloSuperAdminGestionaAdministradores;
        }

        var propios = await permisosActuales.ObtenerAsync(cancellationToken);
        var delObjetivo = await calculadora.CalcularAsync(objetivo.Id, aunqueEsteInactivo: true, cancellationToken);

        return delObjetivo.All(propios.Contains) ? null : ErroresGestion.ObjetivoConMasPermisos;
    }
}
