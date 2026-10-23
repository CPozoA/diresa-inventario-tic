using Inventario.Application.Common.Resultados;

namespace Inventario.Application.Cuenta;

/// <summary>Errores esperados de la sesión y las contraseñas. El <c>Codigo</c> es estable para el frontend.</summary>
public static class ErroresCuenta
{
    public static readonly ErrorAplicacion DatosIncompletos =
        ErrorAplicacion.Validacion("Cuenta.DatosIncompletos", "Complete todos los campos.");

    // Mismo mensaje si el usuario no existe o la contraseña es incorrecta: no revela qué cuentas existen.
    public static readonly ErrorAplicacion CredencialesInvalidas =
        ErrorAplicacion.NoAutenticado("Cuenta.CredencialesInvalidas", "Usuario o contraseña incorrectos.");

    public static readonly ErrorAplicacion Inactiva =
        ErrorAplicacion.Prohibido("Cuenta.Inactiva", "Su cuenta está desactivada. Comuníquese con el administrador de su sede.");

    public static readonly ErrorAplicacion SedeInactiva =
        ErrorAplicacion.Prohibido("Cuenta.SedeInactiva", "Su sede está desactivada. Comuníquese con el administrador del sistema.");

    public static readonly ErrorAplicacion SinSesion =
        ErrorAplicacion.NoAutenticado("Cuenta.SinSesion", "Debe iniciar sesión.");

    public static readonly ErrorAplicacion ContrasenaIgual =
        ErrorAplicacion.Validacion("Cuenta.ContrasenaIgual", "La contraseña nueva debe ser distinta de la actual.");

    public static ErrorAplicacion Bloqueada(int minutosRestantes) =>
        ErrorAplicacion.ReglaDeNegocio(
            "Cuenta.Bloqueada",
            $"Su cuenta está bloqueada por intentos fallidos. Intente de nuevo en {minutosRestantes} minuto(s) o pida el desbloqueo a su administrador.");
}
