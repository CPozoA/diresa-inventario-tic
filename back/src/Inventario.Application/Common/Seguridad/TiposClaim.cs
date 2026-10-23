namespace Inventario.Application.Common.Seguridad;

/// <summary>
/// Datos propios del sistema que viajan en la sesión del usuario (claims), además del identificador y el nombre
/// que pone Identity. Los escribe la infraestructura al iniciar sesión y los lee <see cref="IUsuarioActual"/>.
/// </summary>
public static class TiposClaim
{
    /// <summary>Identificador de la sede del usuario. No existe para el superadministrador.</summary>
    public const string Sede = "inventario:sede";

    /// <summary>Presente con valor "True" solo para el superadministrador.</summary>
    public const string SuperAdmin = "inventario:superadmin";

    /// <summary>Presente con valor "True" mientras el usuario deba cambiar su contraseña temporal.</summary>
    public const string DebeCambiarContrasena = "inventario:debe-cambiar-contrasena";
}
