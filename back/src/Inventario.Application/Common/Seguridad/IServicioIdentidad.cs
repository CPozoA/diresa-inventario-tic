using Inventario.Application.Common.Resultados;

namespace Inventario.Application.Common.Seguridad;

public enum ResultadoVerificacion
{
    Correcta,
    Incorrecta,
    Bloqueada,
}

/// <summary>Resultado de comprobar usuario y contraseña.</summary>
/// <param name="Resultado">Si las credenciales son correctas, incorrectas o la cuenta está bloqueada.</param>
/// <param name="UsuarioId">Usuario encontrado (solo si es correcta o está bloqueada).</param>
/// <param name="BloqueadaHasta">Fin del bloqueo, si está bloqueada.</param>
public sealed record VerificacionCredenciales(ResultadoVerificacion Resultado, Guid? UsuarioId, DateTimeOffset? BloqueadaHasta);

/// <summary>Token para restablecer la contraseña, ya codificado para viajar en un enlace.</summary>
public sealed record SolicitudRestablecimiento(Guid UsuarioId, string Correo, string Token);

/// <summary>Datos de la cuenta (credenciales) que la interfaz necesita mostrar.</summary>
public sealed record DatosCuenta(string NombreUsuario, string? Correo, bool DebeCambiarContrasena, bool Bloqueada);

/// <summary>
/// Operaciones sobre credenciales y sesión. Lo implementa la infraestructura con ASP.NET Core Identity;
/// los casos de uso no conocen Identity ni las cookies.
/// </summary>
public interface IServicioIdentidad
{
    /// <summary>Comprueba usuario (o correo) y contraseña. Cada fallo suma un intento; al tercero bloquea la cuenta.</summary>
    Task<VerificacionCredenciales> VerificarCredencialesAsync(string usuarioOCorreo, string contrasena);

    /// <summary>Abre la sesión (cookie) del usuario. Llamar solo después de verificar credenciales y estado.</summary>
    Task IniciarSesionAsync(Guid usuarioId);

    Task CerrarSesionAsync();

    /// <summary>Cambia la contraseña conociendo la actual. Quita la marca de cambio obligatorio y renueva la sesión.</summary>
    Task<Result> CambiarContrasenaAsync(Guid usuarioId, string contrasenaActual, string contrasenaNueva);

    /// <summary>Genera un token de restablecimiento si existe una cuenta con ese correo; si no, devuelve nulo.</summary>
    Task<SolicitudRestablecimiento?> GenerarTokenRestablecimientoAsync(string correo);

    /// <summary>Restablece la contraseña con el token del correo. También quita el bloqueo.</summary>
    Task<Result> RestablecerContrasenaAsync(Guid usuarioId, string token, string contrasenaNueva);

    Task<DatosCuenta?> ObtenerDatosCuentaAsync(Guid usuarioId);

    // ---- Gestión de cuentas por un administrador (Paso 4B) ----

    Task<IReadOnlyDictionary<Guid, DatosCuenta>> ObtenerDatosCuentasAsync(IReadOnlyCollection<Guid> usuarioIds, CancellationToken cancellationToken);

    Task<bool> ExisteNombreUsuarioAsync(string nombreUsuario);

    /// <summary>Verdadero si otra cuenta (distinta de <paramref name="excluirUsuarioId"/>) ya usa ese correo.</summary>
    Task<bool> ExisteCorreoAsync(string correo, Guid? excluirUsuarioId);

    /// <summary>
    /// Crea la cuenta (credenciales) de un usuario ya agregado al contexto, con una contraseña temporal que deberá cambiar.
    /// Guarda en una sola operación el usuario, sus roles y su cuenta.
    /// </summary>
    Task<Result> CrearCuentaAsync(Guid usuarioId, string nombreUsuario, string correo, string contrasenaTemporal);

    /// <summary>Cambia el correo y guarda también los demás cambios pendientes del usuario.</summary>
    Task<Result> CambiarCorreoAsync(Guid usuarioId, string correo);

    /// <summary>
    /// Pone una contraseña temporal (restablecimiento por el administrador): quita el bloqueo, exige cambiarla
    /// al entrar y cierra las sesiones abiertas del usuario.
    /// </summary>
    Task<Result> EstablecerContrasenaTemporalAsync(Guid usuarioId, string contrasenaTemporal);

    Task DesbloquearAsync(Guid usuarioId);

    /// <summary>Cierra todas las sesiones abiertas del usuario (en menos de un minuto).</summary>
    Task InvalidarSesionesAsync(Guid usuarioId);
}
