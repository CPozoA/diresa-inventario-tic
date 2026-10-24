namespace Inventario.Infrastructure.Persistencia;

/// <summary>Esquemas de SQL Server. Agrupan las tablas por área y facilitan dar permisos por esquema.</summary>
internal static class Esquemas
{
    /// <summary>Sedes y establecimientos.</summary>
    public const string Organizacion = "organizacion";

    /// <summary>Listas de valores: áreas de cada sede y catálogos generales.</summary>
    public const string Catalogos = "catalogos";

    /// <summary>Usuarios, cuentas, roles, permisos y claves de protección de datos.</summary>
    public const string Seguridad = "seguridad";
}
