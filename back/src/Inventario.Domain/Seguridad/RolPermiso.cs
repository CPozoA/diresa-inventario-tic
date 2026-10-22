namespace Inventario.Domain.Seguridad;

/// <summary>Permiso otorgado a un rol de sede.</summary>
public sealed class RolPermiso
{
    // Requerido por EF Core.
    private RolPermiso()
    {
    }

    internal RolPermiso(Guid rolId, string codigoPermiso)
    {
        RolId = rolId;
        CodigoPermiso = codigoPermiso;
    }

    public Guid RolId { get; private set; }

    public string CodigoPermiso { get; private set; } = string.Empty;
}
