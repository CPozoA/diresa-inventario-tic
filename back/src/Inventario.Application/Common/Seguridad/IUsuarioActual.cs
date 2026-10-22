namespace Inventario.Application.Common.Seguridad;

/// <summary>
/// Quién hace la petición. Se obtiene siempre de la sesión en el servidor, nunca de datos que envía el navegador.
/// Es la base del aislamiento entre sedes (docs/ARQUITECTURA.md §5.6).
/// </summary>
public interface IUsuarioActual
{
    bool EstaAutenticado { get; }

    Guid? UsuarioId { get; }

    /// <summary>Sede del usuario. Es nula para el superadministrador.</summary>
    Guid? SedeId { get; }

    bool EsSuperAdmin { get; }

    bool TienePermiso(string codigoPermiso);
}
