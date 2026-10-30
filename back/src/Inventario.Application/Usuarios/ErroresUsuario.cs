using Inventario.Application.Common.Resultados;

namespace Inventario.Application.Usuarios;

/// <summary>Errores esperados de la gestión de usuarios.</summary>
public static class ErroresUsuario
{
    public static readonly ErrorAplicacion NoEncontrado =
        ErrorAplicacion.NoEncontrado("Usuarios.NoEncontrado", "El usuario no existe.");

    public static readonly ErrorAplicacion NombreUsuarioNoValido =
        ErrorAplicacion.Validacion(
            "Usuarios.NombreUsuarioNoValido",
            "El nombre de usuario debe tener entre 3 y 50 caracteres: letras sin tilde, números y . _ - @");

    public static readonly ErrorAplicacion CorreoObligatorio =
        ErrorAplicacion.Validacion("Usuarios.CorreoObligatorio", "El correo es obligatorio: se usa para recuperar la contraseña.");

    public static readonly ErrorAplicacion NombreUsuarioDuplicado =
        ErrorAplicacion.Conflicto("Usuarios.NombreUsuarioDuplicado", "Ya existe una cuenta con ese nombre de usuario.");

    public static readonly ErrorAplicacion CorreoDuplicado =
        ErrorAplicacion.Conflicto("Usuarios.CorreoDuplicado", "Ya existe una cuenta con ese correo.");

    public static readonly ErrorAplicacion DniInvalido =
        ErrorAplicacion.Validacion("Usuarios.DniInvalido", "El DNI debe tener 8 dígitos.");

    public static readonly ErrorAplicacion DniObligatorio =
        ErrorAplicacion.Validacion("Usuarios.DniObligatorio", "Escriba el DNI del usuario.");

    public static readonly ErrorAplicacion DniDuplicado =
        ErrorAplicacion.Conflicto("Usuarios.DniDuplicado", "Ya hay un usuario con ese DNI.");

    public static readonly ErrorAplicacion RolNoEncontrado =
        ErrorAplicacion.Validacion("Usuarios.RolNoEncontrado", "Uno de los roles indicados no existe.");
}
