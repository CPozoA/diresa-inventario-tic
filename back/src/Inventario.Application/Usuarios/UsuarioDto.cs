using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Seguridad;
using Inventario.Domain.Usuarios;

namespace Inventario.Application.Usuarios;

/// <summary>Rol asignado a un usuario.</summary>
public sealed record RolAsignadoDto(Guid Id, string Nombre);

/// <summary>Usuario tal como lo ve la pantalla de usuarios: datos de negocio más el estado de su cuenta.</summary>
public sealed record UsuarioDto(
    Guid Id,
    Guid SedeId,
    string NombreUsuario,
    string? Correo,
    string Nombres,
    string Apellidos,
    string? Cargo,
    bool Activo,
    bool Bloqueado,
    bool DebeCambiarContrasena,
    IReadOnlyList<RolAsignadoDto> Roles);

internal static class MapeoUsuario
{
    public static UsuarioDto ADto(Usuario usuario, DatosCuenta? cuenta, IReadOnlyDictionary<Guid, Rol> roles) => new(
        usuario.Id,
        usuario.SedeId ?? Guid.Empty,
        cuenta?.NombreUsuario ?? string.Empty,
        cuenta?.Correo,
        usuario.Nombres,
        usuario.Apellidos,
        usuario.Cargo,
        usuario.Activo,
        cuenta?.Bloqueada ?? false,
        cuenta?.DebeCambiarContrasena ?? false,
        [.. usuario.Roles
            .Select(ur => roles.TryGetValue(ur.RolId, out var rol) ? new RolAsignadoDto(rol.Id, rol.Nombre) : null)
            .OfType<RolAsignadoDto>()
            .OrderBy(r => r.Nombre, StringComparer.CurrentCultureIgnoreCase)]);
}
