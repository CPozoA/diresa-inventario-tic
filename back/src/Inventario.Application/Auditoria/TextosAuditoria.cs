using Inventario.Application.Common.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Auditoria;

/// <summary>Datos que se repiten en las acciones de la auditoría.</summary>
internal static class TextosAuditoria
{
    /// <summary>Código patrimonial del activo, para nombrarlo en la acción ("PAT-0001").</summary>
    public static async Task<string> CodigoActivoAsync(this IAppDbContext db, Guid activoId, CancellationToken cancellationToken) =>
        await db.Activos.AsNoTracking()
            .Where(a => a.Id == activoId)
            .Select(a => a.CodigoPatrimonial)
            .FirstOrDefaultAsync(cancellationToken) ?? "(activo)";
}
