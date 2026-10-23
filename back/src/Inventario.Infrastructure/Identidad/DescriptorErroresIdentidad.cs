using System.Globalization;
using Microsoft.AspNetCore.Identity;

namespace Inventario.Infrastructure.Identidad;

/// <summary>Mensajes de error de Identity en español (por defecto vienen en inglés).</summary>
internal sealed class DescriptorErroresIdentidad : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), string.Create(CultureInfo.InvariantCulture, $"La contraseña debe tener al menos {length} caracteres."));

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "La contraseña debe tener al menos un número.");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "La contraseña debe tener al menos una letra minúscula.");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "La contraseña debe tener al menos una letra mayúscula.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe tener al menos un símbolo.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), string.Create(CultureInfo.InvariantCulture, $"La contraseña debe tener al menos {uniqueChars} caracteres distintos."));

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "La contraseña no es correcta.");

    public override IdentityError InvalidToken() =>
        Error(nameof(InvalidToken), "El enlace no es válido o ya venció.");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), $"El nombre de usuario '{userName}' ya está en uso.");

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), $"El correo '{email}' ya está registrado.");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), $"El nombre de usuario '{userName}' no es válido. Use solo letras, números y . _ - @");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), $"El correo '{email}' no es válido.");

    public override IdentityError UserLockoutNotEnabled() =>
        Error(nameof(UserLockoutNotEnabled), "El bloqueo no está habilitado para esta cuenta.");

    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Ocurrió un error con la cuenta.");

    private static IdentityError Error(string codigo, string descripcion) =>
        new() { Code = codigo, Description = descripcion };
}
