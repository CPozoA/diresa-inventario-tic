namespace Inventario.Domain.Common;

/// <summary>Validaciones de texto reutilizables por las entidades.</summary>
internal static class Guardas
{
    public static string TextoObligatorio(string? valor, string campo, int longitudMaxima)
    {
        var limpio = valor?.Trim();

        if (string.IsNullOrEmpty(limpio))
        {
            throw new DominioException($"{campo} es obligatorio.");
        }

        if (limpio.Length > longitudMaxima)
        {
            throw new DominioException($"{campo} no puede superar {longitudMaxima} caracteres.");
        }

        return limpio;
    }

    public static string? TextoOpcional(string? valor, string campo, int longitudMaxima)
    {
        var limpio = valor?.Trim();

        if (string.IsNullOrEmpty(limpio))
        {
            return null;
        }

        if (limpio.Length > longitudMaxima)
        {
            throw new DominioException($"{campo} no puede superar {longitudMaxima} caracteres.");
        }

        return limpio;
    }
}
