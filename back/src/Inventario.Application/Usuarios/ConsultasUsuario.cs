using Inventario.Application.Common.Persistencia;
using Inventario.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Usuarios;

internal static class ConsultasUsuario
{
    /// <summary>
    /// Usuario a gestionar, con sus roles y con seguimiento de cambios. El filtro de sede hace que un usuario
    /// de otra sede "no exista" (404).
    /// </summary>
    public static Task<Usuario?> BuscarParaGestionAsync(this IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Usuarios
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
}
