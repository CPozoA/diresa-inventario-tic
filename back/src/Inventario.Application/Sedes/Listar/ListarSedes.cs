using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Sedes.Listar;

/// <summary>Sede tal como la ve la interfaz.</summary>
public sealed record SedeDto(Guid Id, string Codigo, string Nombre, bool Activa);

/// <summary>Lista las sedes ordenadas por nombre.</summary>
/// <remarks>En el Paso 4 se restringe al superadministrador.</remarks>
public sealed record ListarSedesQuery : IQuery<IReadOnlyList<SedeDto>>;

internal sealed class ListarSedesHandler(IAppDbContext db) : IQueryHandler<ListarSedesQuery, IReadOnlyList<SedeDto>>
{
    public async Task<Result<IReadOnlyList<SedeDto>>> Handle(ListarSedesQuery query, CancellationToken cancellationToken)
    {
        var sedes = await db.Sedes
            .AsNoTracking()
            .OrderBy(s => s.Nombre)
            .Select(s => new SedeDto(s.Id, s.Codigo, s.Nombre, s.Activa))
            .ToListAsync(cancellationToken);

        return Result.Exito<IReadOnlyList<SedeDto>>(sedes);
    }
}
