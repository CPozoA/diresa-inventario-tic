using Inventario.Domain.Sedes;
using Inventario.Domain.Seguridad;
using Inventario.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Common.Persistencia;

/// <summary>
/// Acceso a datos para los casos de uso. Lo implementa el contexto de EF Core de Infrastructure.
/// Solo expone las raíces de agregado: los permisos de un rol se cambian a través del rol
/// y los roles de un usuario a través del usuario. Ver docs/ARQUITECTURA.md §5.4.
/// </summary>
public interface IAppDbContext
{
    DbSet<Sede> Sedes { get; }

    DbSet<Usuario> Usuarios { get; }

    DbSet<Rol> Roles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
