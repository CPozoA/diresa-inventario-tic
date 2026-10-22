namespace Inventario.Domain.Seguridad;

public enum TipoRol
{
    /// <summary>Rol del sistema con todos los permisos y alcance sobre todas las sedes.</summary>
    SuperAdmin = 1,

    /// <summary>Rol del sistema con todos los permisos de sede. Solo lo asigna el superadministrador.</summary>
    AdministradorSede = 2,

    /// <summary>Rol creado y administrado por una sede, con los permisos que esta elija.</summary>
    DeSede = 3,
}
