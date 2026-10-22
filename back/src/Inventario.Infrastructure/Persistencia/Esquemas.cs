namespace Inventario.Infrastructure.Persistencia;

/// <summary>Esquemas de SQL Server. Agrupan las tablas por área y facilitan dar permisos por esquema.</summary>
internal static class Esquemas
{
    /// <summary>Sedes y, más adelante, catálogos.</summary>
    public const string Organizacion = "organizacion";

    /// <summary>Usuarios, cuentas, roles, permisos y claves de protección de datos.</summary>
    public const string Seguridad = "seguridad";
}
