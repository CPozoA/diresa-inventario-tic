namespace Inventario.Domain.Seguridad;

/// <summary>
/// Roles predefinidos. Los del sistema tienen identificadores fijos; los iniciales de cada sede
/// (Editor y Consulta) se crean al dar de alta la sede y luego la sede puede modificarlos.
/// </summary>
public static class RolesDelSistema
{
    public static readonly Guid SuperAdminId = new("019a0b6e-0000-7000-8000-0000000000a1");
    public static readonly Guid AdministradorSedeId = new("019a0b6e-0000-7000-8000-0000000000a2");

    public static IReadOnlyList<Rol> Crear() =>
    [
        Rol.CrearDelSistema(
            SuperAdminId,
            TipoRol.SuperAdmin,
            "Superadministrador",
            "Acceso total a todas las sedes."),
        Rol.CrearDelSistema(
            AdministradorSedeId,
            TipoRol.AdministradorSede,
            "Administrador de sede",
            "Gestiona todo el inventario, los usuarios y los roles de su sede."),
    ];

    /// <summary>Roles con los que arranca cada sede nueva. La sede puede modificarlos después.</summary>
    public static IReadOnlyList<Rol> CrearInicialesDeSede(Guid sedeId) =>
    [
        Rol.CrearDeSede(
            sedeId,
            "Editor",
            "Registra y modifica el inventario de la sede.",
            [
                Permisos.Activos.Ver, Permisos.Activos.Crear, Permisos.Activos.Editar,
                Permisos.Licencias.Ver, Permisos.Licencias.Gestionar,
                Permisos.Mantenimiento.Ver, Permisos.Mantenimiento.Gestionar,
                Permisos.Asignaciones.Ver, Permisos.Asignaciones.Gestionar,
            ]),
        Rol.CrearDeSede(
            sedeId,
            "Consulta",
            "Solo puede ver el inventario de la sede.",
            [
                Permisos.Activos.Ver,
                Permisos.Licencias.Ver,
                Permisos.Mantenimiento.Ver,
                Permisos.Asignaciones.Ver,
            ]),
    ];
}
