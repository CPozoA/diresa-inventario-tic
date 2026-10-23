namespace Inventario.Application.Common.Resultados;

/// <summary>Clase de error. La capa Api la traduce al código HTTP correspondiente.</summary>
public enum TipoError
{
    /// <summary>Datos de entrada inválidos (HTTP 400).</summary>
    Validacion,

    /// <summary>Falta iniciar sesión o las credenciales son incorrectas (HTTP 401).</summary>
    NoAutenticado,

    /// <summary>El registro no existe o no es visible para el usuario (HTTP 404).</summary>
    NoEncontrado,

    /// <summary>Choca con el estado actual, por ejemplo un código repetido (HTTP 409).</summary>
    Conflicto,

    /// <summary>El usuario está identificado, pero no tiene permiso (HTTP 403).</summary>
    Prohibido,

    /// <summary>Una regla de negocio impide la operación (HTTP 422).</summary>
    ReglaDeNegocio,
}

/// <summary>Error esperado de un caso de uso.</summary>
/// <param name="Codigo">Identificador estable para el frontend, por ejemplo <c>Activos.CodigoDuplicado</c>.</param>
/// <param name="Mensaje">Texto en español para mostrar al usuario.</param>
/// <param name="Tipo">Clase de error.</param>
public sealed record ErrorAplicacion(string Codigo, string Mensaje, TipoError Tipo)
{
    public static ErrorAplicacion Validacion(string codigo, string mensaje) => new(codigo, mensaje, TipoError.Validacion);

    public static ErrorAplicacion NoAutenticado(string codigo, string mensaje) => new(codigo, mensaje, TipoError.NoAutenticado);

    public static ErrorAplicacion NoEncontrado(string codigo, string mensaje) => new(codigo, mensaje, TipoError.NoEncontrado);

    public static ErrorAplicacion Conflicto(string codigo, string mensaje) => new(codigo, mensaje, TipoError.Conflicto);

    public static ErrorAplicacion Prohibido(string codigo, string mensaje) => new(codigo, mensaje, TipoError.Prohibido);

    public static ErrorAplicacion ReglaDeNegocio(string codigo, string mensaje) => new(codigo, mensaje, TipoError.ReglaDeNegocio);
}
