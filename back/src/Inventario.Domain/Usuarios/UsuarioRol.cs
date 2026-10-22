namespace Inventario.Domain.Usuarios;

/// <summary>Rol asignado a un usuario. Un usuario puede tener varios (D16).</summary>
public sealed class UsuarioRol
{
    // Requerido por EF Core.
    private UsuarioRol()
    {
    }

    internal UsuarioRol(Guid usuarioId, Guid rolId)
    {
        UsuarioId = usuarioId;
        RolId = rolId;
    }

    public Guid UsuarioId { get; private set; }

    public Guid RolId { get; private set; }
}
