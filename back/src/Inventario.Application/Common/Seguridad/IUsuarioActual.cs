namespace Inventario.Application.Common.Seguridad;

/// <summary>
/// Quién hace la petición. Se obtiene siempre de la sesión en el servidor, nunca de datos que envía el navegador.
/// Es la base del aislamiento entre sedes (docs/ARQUITECTURA.md §5.6).
/// Los permisos se consultan aparte, con <see cref="IPermisosUsuarioActual"/>.
/// </summary>
public interface IUsuarioActual
{
    bool EstaAutenticado { get; }

    Guid? UsuarioId { get; }

    /// <summary>Sede del usuario. Es nula para el superadministrador.</summary>
    Guid? SedeId { get; }

    bool EsSuperAdmin { get; }

    /// <summary>
    /// Verdadero cuando no hay una petición HTTP de por medio: tareas del propio sistema
    /// (carga de datos iniciales, migraciones, herramientas de diseño). No aplica el filtro por sede.
    /// Una petición anónima NO es un proceso interno: no ve ni puede modificar datos de ninguna sede.
    /// </summary>
    bool EsProcesoInterno { get; }
}
