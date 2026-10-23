namespace Inventario.Application.Common.Resultados;

/// <summary>Identificador del registro recién creado. La Api lo devuelve con HTTP 201 Created.</summary>
public sealed record IdCreado(Guid Id);
