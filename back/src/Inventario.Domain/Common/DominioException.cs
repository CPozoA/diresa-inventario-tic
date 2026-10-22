namespace Inventario.Domain.Common;

/// <summary>
/// Se lanza cuando una operación violaría una regla del negocio
/// (por ejemplo, asignar a un usuario un rol de otra sede).
/// La capa Application valida antes de llegar aquí; esta excepción es la última barrera.
/// </summary>
public sealed class DominioException : Exception
{
    public DominioException()
    {
    }

    public DominioException(string message)
        : base(message)
    {
    }

    public DominioException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
