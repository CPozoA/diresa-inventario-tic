using Inventario.Domain.Activos;
using Inventario.Domain.Asignaciones;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Establecimientos;
using Inventario.Domain.Mantenimientos;
using Inventario.Domain.Personal;
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

    DbSet<Establecimiento> Establecimientos { get; }

    DbSet<Activo> Activos { get; }

    DbSet<FotoActivo> FotosActivo { get; }

    DbSet<Persona> Personal { get; }

    DbSet<Asignacion> Asignaciones { get; }

    DbSet<Mantenimiento> Mantenimientos { get; }

    // Catálogos de cada sede.
    DbSet<Area> Areas { get; }

    // Catálogos generales.
    DbSet<TipoActivo> TiposActivo { get; }

    DbSet<Marca> Marcas { get; }

    DbSet<EstadoActivo> EstadosActivo { get; }

    DbSet<SistemaOperativo> SistemasOperativos { get; }

    DbSet<Procesador> Procesadores { get; }

    DbSet<Antivirus> Antivirus { get; }

    DbSet<TipoLicencia> TiposLicencia { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
