namespace Inventario.Domain.Common;

/// <summary>
/// Marca una entidad que siempre pertenece a una sede (activos, licencias, áreas…).
/// La infraestructura usa esta marca para filtrar automáticamente por la sede del usuario
/// y para impedir que el registro cambie de sede (no existen transferencias).
/// Ver docs/ARQUITECTURA.md §5.6.
/// </summary>
public interface IPerteneceASede
{
    Guid SedeId { get; }
}
