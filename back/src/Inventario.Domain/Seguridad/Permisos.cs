namespace Inventario.Domain.Seguridad;

/// <summary>Descripción de un permiso del sistema.</summary>
/// <param name="Codigo">Identificador estable, por ejemplo <c>Activos.Crear</c>. Se guarda en la base de datos.</param>
/// <param name="Modulo">Agrupa los permisos en la pantalla de edición de roles.</param>
/// <param name="Descripcion">Texto que ve el administrador al marcar el permiso.</param>
/// <param name="ExclusivoSuperAdmin">Si es verdadero, ningún rol de sede puede tenerlo.</param>
public sealed record DefinicionPermiso(string Codigo, string Modulo, string Descripcion, bool ExclusivoSuperAdmin);

/// <summary>
/// Catálogo de permisos. Los permisos se definen en el código: desde la aplicación solo se combinan en roles.
/// Para agregar uno nuevo: declarar la constante y agregarla a <see cref="Todos"/>.
/// Ver docs/ARQUITECTURA.md §6.2.
/// </summary>
public static class Permisos
{
    public static class Activos
    {
        public const string Ver = "Activos.Ver";
        public const string Crear = "Activos.Crear";
        public const string Editar = "Activos.Editar";
        public const string Eliminar = "Activos.Eliminar";
    }

    public static class Licencias
    {
        public const string Ver = "Licencias.Ver";
        public const string Gestionar = "Licencias.Gestionar";
    }

    public static class Mantenimiento
    {
        public const string Ver = "Mantenimiento.Ver";
        public const string Gestionar = "Mantenimiento.Gestionar";
    }

    public static class Asignaciones
    {
        public const string Ver = "Asignaciones.Ver";
        public const string Gestionar = "Asignaciones.Gestionar";
    }

    public static class Catalogos
    {
        public const string GestionarSede = "Catalogos.GestionarSede";
        public const string GestionarGlobales = "Catalogos.GestionarGlobales";
    }

    public static class Usuarios
    {
        public const string Ver = "Usuarios.Ver";
        public const string Gestionar = "Usuarios.Gestionar";
    }

    public static class Roles
    {
        public const string Ver = "Roles.Ver";
        public const string Gestionar = "Roles.Gestionar";
    }

    public static class Auditoria
    {
        public const string Ver = "Auditoria.Ver";
    }

    public static class Sedes
    {
        public const string Gestionar = "Sedes.Gestionar";
    }

    public static class Establecimientos
    {
        public const string Gestionar = "Establecimientos.Gestionar";
    }

    public static class Administradores
    {
        public const string Gestionar = "Administradores.Gestionar";
    }

    public static IReadOnlyList<DefinicionPermiso> Todos { get; } =
    [
        new(Activos.Ver, "Activos", "Ver activos y sus fichas", false),
        new(Activos.Crear, "Activos", "Registrar activos", false),
        new(Activos.Editar, "Activos", "Modificar activos, discos y memoria", false),
        new(Activos.Eliminar, "Activos", "Eliminar activos", false),

        new(Licencias.Ver, "Licencias", "Ver licencias de software", false),
        new(Licencias.Gestionar, "Licencias", "Registrar y modificar licencias", false),

        new(Mantenimiento.Ver, "Mantenimiento", "Ver la bitácora de mantenimiento", false),
        new(Mantenimiento.Gestionar, "Mantenimiento", "Registrar y modificar mantenimientos", false),

        new(Asignaciones.Ver, "Asignaciones", "Ver asignaciones de equipos", false),
        new(Asignaciones.Gestionar, "Asignaciones", "Registrar entregas y devoluciones", false),

        new(Catalogos.GestionarSede, "Catálogos", "Administrar las áreas de la sede", false),
        new(Usuarios.Ver, "Usuarios", "Ver usuarios de la sede", false),
        new(Usuarios.Gestionar, "Usuarios", "Crear, desactivar y restablecer usuarios de la sede", false),
        new(Roles.Ver, "Roles", "Ver roles de la sede", false),
        new(Roles.Gestionar, "Roles", "Crear y modificar roles de la sede", false),
        new(Auditoria.Ver, "Auditoría", "Ver el registro de auditoría de la sede (quién hizo qué y cuándo)", false),

        new(Catalogos.GestionarGlobales, "Administración general", "Administrar catálogos comunes a todas las sedes", true),
        new(Sedes.Gestionar, "Administración general", "Crear y modificar sedes", true),
        new(Establecimientos.Gestionar, "Administración general", "Registrar, importar y modificar establecimientos", true),
        new(Administradores.Gestionar, "Administración general", "Asignar administradores de sede", true),
    ];

    private static readonly Dictionary<string, DefinicionPermiso> PorCodigo =
        Todos.ToDictionary(p => p.Codigo, StringComparer.Ordinal);

    /// <summary>Códigos de todos los permisos que puede tener un rol de sede.</summary>
    public static IReadOnlySet<string> DeSede { get; } =
        Todos.Where(p => !p.ExclusivoSuperAdmin).Select(p => p.Codigo).ToHashSet(StringComparer.Ordinal);

    /// <summary>Códigos de todos los permisos del sistema.</summary>
    public static IReadOnlySet<string> CodigosTodos { get; } =
        Todos.Select(p => p.Codigo).ToHashSet(StringComparer.Ordinal);

    public static DefinicionPermiso? Buscar(string codigo) =>
        PorCodigo.GetValueOrDefault(codigo);
}
