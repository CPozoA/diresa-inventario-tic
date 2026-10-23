using Inventario.Application.Common.Resultados;

namespace Inventario.Application.Roles;

/// <summary>Errores esperados de la gestión de roles.</summary>
public static class ErroresRol
{
    public static readonly ErrorAplicacion NoEncontrado =
        ErrorAplicacion.NoEncontrado("Roles.NoEncontrado", "El rol no existe.");

    public static readonly ErrorAplicacion NombreDuplicado =
        ErrorAplicacion.Conflicto("Roles.NombreDuplicado", "Ya existe un rol con ese nombre en la sede.");

    public static readonly ErrorAplicacion DelSistema =
        ErrorAplicacion.ReglaDeNegocio("Roles.DelSistema", "Los roles del sistema no se pueden modificar ni eliminar.");

    public static readonly ErrorAplicacion EnUso =
        ErrorAplicacion.Conflicto(
            "Roles.EnUso",
            "El rol está asignado a uno o más usuarios. Quíteselo antes de eliminarlo.");

    public static readonly ErrorAplicacion ConMasPermisos =
        ErrorAplicacion.Prohibido(
            "Roles.ConMasPermisos",
            "No puede modificar ni eliminar un rol que tiene permisos que usted no tiene.");
}
