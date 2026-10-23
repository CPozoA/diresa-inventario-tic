namespace Inventario.Api.IntegrationTests.Infraestructura;

/// <summary>Usuarios y contraseñas que se crean en la base de pruebas. Solo existen ahí.</summary>
public static class DatosPrueba
{
    public const string Contrasena = "Prueba-2026x";

    public const string SuperAdmin = "superadmin.pruebas";

    /// <summary>Editor de la Red de Salud Huanta.</summary>
    public const string EditorHuanta = "editor.huanta";

    /// <summary>Editor de la Red de Salud Puquio.</summary>
    public const string EditorPuquio = "editor.puquio";

    /// <summary>Usuario de Huanta con contraseña temporal (debe cambiarla).</summary>
    public const string TemporalHuanta = "temporal.huanta";

    /// <summary>Administrador de la Red de Salud Huanta (rol del sistema Administrador de sede).</summary>
    public const string AdminHuanta = "admin.huanta";

    /// <summary>
    /// Usuario de Huanta con un rol propio que gestiona usuarios y roles, pero del inventario solo ve activos.
    /// Sirve para probar que nadie otorga ni gestiona permisos que no tiene.
    /// </summary>
    public const string GestorHuanta = "gestor.huanta";

    /// <summary>Nombre del rol propio de <see cref="GestorHuanta"/>.</summary>
    public const string RolGestor = "Gestor de pruebas";

    public const string CodigoHuanta = "HUANTA";

    public const string CodigoPuquio = "PUQUIO";

    /// <summary>Nombre del rol propio que se crea en cada sede para las pruebas.</summary>
    public const string RolTecnico = "Técnico de pruebas";

    /// <summary>
    /// Cadena de conexión de la base de pruebas. Se puede cambiar con la variable de entorno INVENTARIO_PRUEBAS_SQL.
    /// </summary>
    public static string CadenaConexion =>
        Environment.GetEnvironmentVariable("INVENTARIO_PRUEBAS_SQL")
        ?? @"Server=.\SQLEXPRESS;Database=InventarioTIC_Pruebas;Trusted_Connection=True;TrustServerCertificate=True";
}
