namespace Inventario.Domain.Common;

/// <summary>
/// Base de toda entidad con identidad propia.
/// Los identificadores son GUID versión 7: únicos y ordenables por fecha de creación,
/// lo que mantiene eficientes los índices de SQL Server.
/// </summary>
public abstract class Entidad
{
    protected Entidad()
        : this(Guid.CreateVersion7())
    {
    }

    protected Entidad(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DominioException("El identificador de una entidad no puede estar vacío.");
        }

        Id = id;
    }

    public Guid Id { get; private set; }
}
