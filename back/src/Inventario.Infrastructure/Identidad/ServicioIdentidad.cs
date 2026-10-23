using System.Text;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Inventario.Infrastructure.Identidad;

/// <summary>Implementación de <see cref="IServicioIdentidad"/> con ASP.NET Core Identity y cookie de sesión.</summary>
internal sealed class ServicioIdentidad(UserManager<CuentaUsuario> cuentas, SignInManager<CuentaUsuario> sesiones)
    : IServicioIdentidad
{
    private static readonly ErrorAplicacion TokenInvalido = ErrorAplicacion.Validacion(
        "Cuenta.EnlaceInvalido",
        "El enlace para restablecer la contraseña no es válido o ya venció. Solicite uno nuevo.");

    public async Task<VerificacionCredenciales> VerificarCredencialesAsync(string usuarioOCorreo, string contrasena)
    {
        var cuenta = await cuentas.FindByNameAsync(usuarioOCorreo) ?? await cuentas.FindByEmailAsync(usuarioOCorreo);
        if (cuenta is null)
        {
            return new VerificacionCredenciales(ResultadoVerificacion.Incorrecta, null, null);
        }

        // lockoutOnFailure: cada fallo suma un intento; al llegar al máximo (3) la cuenta queda bloqueada.
        var resultado = await sesiones.CheckPasswordSignInAsync(cuenta, contrasena, lockoutOnFailure: true);

        if (resultado.IsLockedOut)
        {
            return new VerificacionCredenciales(ResultadoVerificacion.Bloqueada, cuenta.Id, cuenta.LockoutEnd);
        }

        return resultado.Succeeded
            ? new VerificacionCredenciales(ResultadoVerificacion.Correcta, cuenta.Id, null)
            : new VerificacionCredenciales(ResultadoVerificacion.Incorrecta, null, null);
    }

    public async Task IniciarSesionAsync(Guid usuarioId)
    {
        var cuenta = await BuscarObligatoriaAsync(usuarioId);
        await sesiones.SignInAsync(cuenta, isPersistent: false);
    }

    public Task CerrarSesionAsync() => sesiones.SignOutAsync();

    public async Task<Result> CambiarContrasenaAsync(Guid usuarioId, string contrasenaActual, string contrasenaNueva)
    {
        var cuenta = await BuscarObligatoriaAsync(usuarioId);

        var resultado = await cuentas.ChangePasswordAsync(cuenta, contrasenaActual, contrasenaNueva);
        if (!resultado.Succeeded)
        {
            return Result.Fallo(AErrorAplicacion(resultado));
        }

        cuenta.DebeCambiarContrasena = false;
        await cuentas.UpdateAsync(cuenta);

        // Vuelve a emitir la cookie: quita la marca de "debe cambiar contraseña" de la sesión actual.
        await sesiones.RefreshSignInAsync(cuenta);
        return Result.Exito();
    }

    public async Task<SolicitudRestablecimiento?> GenerarTokenRestablecimientoAsync(string correo)
    {
        var cuenta = await cuentas.FindByEmailAsync(correo);
        if (cuenta?.Email is null)
        {
            return null;
        }

        var token = await cuentas.GeneratePasswordResetTokenAsync(cuenta);
        var tokenParaEnlace = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        return new SolicitudRestablecimiento(cuenta.Id, cuenta.Email, tokenParaEnlace);
    }

    public async Task<Result> RestablecerContrasenaAsync(Guid usuarioId, string token, string contrasenaNueva)
    {
        var cuenta = await cuentas.FindByIdAsync(usuarioId.ToString());
        if (cuenta is null || !TryDecodificarToken(token, out var tokenOriginal))
        {
            return Result.Fallo(TokenInvalido);
        }

        var resultado = await cuentas.ResetPasswordAsync(cuenta, tokenOriginal, contrasenaNueva);
        if (!resultado.Succeeded)
        {
            return Result.Fallo(AErrorAplicacion(resultado));
        }

        // La persona eligió su propia contraseña: ya no debe cambiarla y se levanta cualquier bloqueo.
        cuenta.DebeCambiarContrasena = false;
        await cuentas.UpdateAsync(cuenta);
        await cuentas.SetLockoutEndDateAsync(cuenta, null);
        await cuentas.ResetAccessFailedCountAsync(cuenta);

        return Result.Exito();
    }

    public async Task<DatosCuenta?> ObtenerDatosCuentaAsync(Guid usuarioId)
    {
        var cuenta = await cuentas.FindByIdAsync(usuarioId.ToString());
        return cuenta is null
            ? null
            : new DatosCuenta(cuenta.UserName ?? string.Empty, cuenta.Email, cuenta.DebeCambiarContrasena);
    }

    private async Task<CuentaUsuario> BuscarObligatoriaAsync(Guid usuarioId) =>
        await cuentas.FindByIdAsync(usuarioId.ToString())
        ?? throw new InvalidOperationException($"No existe la cuenta del usuario {usuarioId}.");

    private static bool TryDecodificarToken(string token, out string tokenOriginal)
    {
        try
        {
            tokenOriginal = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            return true;
        }
        catch (FormatException)
        {
            tokenOriginal = string.Empty;
            return false;
        }
    }

    private static ErrorAplicacion AErrorAplicacion(IdentityResult resultado)
    {
        if (resultado.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            return ErrorAplicacion.Validacion("Cuenta.ContrasenaActualIncorrecta", "La contraseña actual no es correcta.");
        }

        if (resultado.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
        {
            return TokenInvalido;
        }

        var mensaje = string.Join(" ", resultado.Errors.Select(e => e.Description));
        return ErrorAplicacion.Validacion("Cuenta.ContrasenaNoValida", mensaje);
    }
}
