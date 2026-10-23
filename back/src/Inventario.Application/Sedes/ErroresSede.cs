using Inventario.Application.Common.Resultados;

namespace Inventario.Application.Sedes;

/// <summary>Errores esperados de la gestión de sedes.</summary>
public static class ErroresSede
{
    public static readonly ErrorAplicacion NoEncontrada =
        ErrorAplicacion.NoEncontrado("Sedes.NoEncontrada", "La sede no existe.");

    public static readonly ErrorAplicacion CodigoDuplicado =
        ErrorAplicacion.Conflicto("Sedes.CodigoDuplicado", "Ya existe una sede con ese código.");
}
