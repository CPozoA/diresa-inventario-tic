using Microsoft.AspNetCore.Identity;

namespace Inventario.Infrastructure.Identidad;

/// <summary>
/// Credenciales de un usuario para ASP.NET Core Identity: nombre de usuario, correo, contraseña (hash),
/// intentos fallidos y bloqueo. Comparte el identificador con <see cref="Domain.Usuarios.Usuario"/>,
/// que guarda los datos de negocio (sede, nombres, roles).
/// </summary>
public sealed class CuentaUsuario : IdentityUser<Guid>
{
    /// <summary>
    /// Verdadero en cuentas nuevas o con contraseña restablecida por un administrador:
    /// el usuario debe cambiarla antes de usar el sistema.
    /// </summary>
    public bool DebeCambiarContrasena { get; set; }
}
