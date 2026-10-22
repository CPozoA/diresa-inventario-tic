namespace Inventario.Application.Common.Resultados;

/// <summary>
/// Resultado de un caso de uso: éxito o un <see cref="ErrorAplicacion"/> esperado.
/// Los errores inesperados no se modelan aquí: se lanzan como excepciones y los atrapa el manejador global.
/// </summary>
/// <example>
/// <code>
/// return Result.Exito(activo.Id);
/// return Result.Fallo&lt;Guid&gt;(ErroresActivo.CodigoDuplicado);
/// return ErroresActivo.CodigoDuplicado; // conversión implícita a Result&lt;T&gt;
/// </code>
/// </example>
public class Result
{
    protected Result(ErrorAplicacion? error)
    {
        Error = error;
    }

    public ErrorAplicacion? Error { get; }

    public bool EsExito => Error is null;

    public static Result Exito() => new(null);

    public static Result Fallo(ErrorAplicacion error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static Result<T> Exito<T>(T valor) => new(valor, null);

    public static Result<T> Fallo<T>(ErrorAplicacion error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }
}

/// <summary>Resultado con un valor cuando la operación tiene éxito.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _valor;

    internal Result(T? valor, ErrorAplicacion? error)
        : base(error)
    {
        _valor = valor;
    }

    /// <summary>El valor. Solo se puede leer si <see cref="Result.EsExito"/> es verdadero.</summary>
    public T Valor => EsExito
        ? _valor!
        : throw new InvalidOperationException("No se puede leer el valor de un resultado fallido.");

    public static implicit operator Result<T>(ErrorAplicacion error) => Fallo<T>(error);

    public static implicit operator Result<T>(T valor) => Exito(valor);
}
