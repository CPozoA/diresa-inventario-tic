using System.Reflection;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Common;
using Inventario.Domain.Establecimientos;
using Inventario.Domain.Sedes;
using Inventario.Domain.Seguridad;
using Inventario.Domain.Usuarios;
using Inventario.Infrastructure.Identidad;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core de toda la aplicación.
/// Hereda de <see cref="IdentityUserContext{TUser, TKey}"/> (cuentas sin la tabla de roles de Identity:
/// los roles son propios, ver docs/ARQUITECTURA.md §6.2) y guarda además las claves de protección de datos.
/// Aplica el <b>filtro global por sede</b> (docs/ARQUITECTURA.md §5.6).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IUsuarioActual usuarioActual)
    : IdentityUserContext<CuentaUsuario, Guid>(options), IAppDbContext, IDataProtectionKeyContext
{
    private static readonly MethodInfo MetodoFiltroSede = typeof(AppDbContext)
        .GetMethod(nameof(AplicarFiltroSede), BindingFlags.Instance | BindingFlags.NonPublic)!;

    public DbSet<Sede> Sedes => Set<Sede>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Rol> Roles => Set<Rol>();

    public DbSet<Establecimiento> Establecimientos => Set<Establecimiento>();

    public DbSet<Area> Areas => Set<Area>();

    public DbSet<TipoActivo> TiposActivo => Set<TipoActivo>();

    public DbSet<Marca> Marcas => Set<Marca>();

    public DbSet<EstadoActivo> EstadosActivo => Set<EstadoActivo>();

    public DbSet<SistemaOperativo> SistemasOperativos => Set<SistemaOperativo>();

    public DbSet<TipoLicencia> TiposLicencia => Set<TipoLicencia>();

    /// <summary>Claves que cifran la cookie de sesión. Se guardan aquí para sobrevivir a los reciclajes de IIS.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // EF Core evalúa estas dos propiedades en cada consulta (no al construir el modelo), por eso el filtro
    // siempre corresponde al usuario de la petición actual.
    private bool SinRestriccionDeSede => usuarioActual.EsSuperAdmin || usuarioActual.EsProcesoInterno;

    private Guid? SedeActualId => usuarioActual.SedeId;

    // EF Core llama a este parámetro "modelBuilder" e Identity lo redeclara como "builder".
#pragma warning disable CA1725 // Los nombres de parámetro deben coincidir con la declaración base.
    protected override void OnModelCreating(ModelBuilder builder)
#pragma warning restore CA1725
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Tablas de Identity con nombres en español y en el esquema de seguridad.
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("CuentaClaims", Esquemas.Seguridad);
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("CuentaLogins", Esquemas.Seguridad);
        builder.Entity<IdentityUserToken<Guid>>().ToTable("CuentaTokens", Esquemas.Seguridad);
        builder.Entity<DataProtectionKey>().ToTable("ClavesProteccionDatos", Esquemas.Seguridad);

        AplicarFiltrosDeSede(builder);
    }

    /// <summary>
    /// Filtro global por sede. El superadministrador y los procesos internos ven todo; un usuario de sede solo ve lo suyo;
    /// una petición anónima no ve nada. Cualquier excepción usa <c>IgnoreQueryFilters()</c> con un comentario que la justifica.
    /// </summary>
    private void AplicarFiltrosDeSede(ModelBuilder builder)
    {
        // Un usuario de sede solo ve su propia sede.
        builder.Entity<Sede>().HasQueryFilter(s => SinRestriccionDeSede || s.Id == SedeActualId);

        // Solo ve los usuarios de su sede (nunca al superadministrador, que no tiene sede).
        builder.Entity<Usuario>().HasQueryFilter(u => SinRestriccionDeSede || (u.SedeId != null && u.SedeId == SedeActualId));

        // Ve los roles de su sede y los del sistema (para saber, por ejemplo, quién es Administrador de sede).
        builder.Entity<Rol>().HasQueryFilter(r => SinRestriccionDeSede
            || (SedeActualId != null && (r.SedeId == null || r.SedeId == SedeActualId)));

        // Toda entidad que pertenece a una sede (activos, licencias, áreas…) recibe el mismo filtro automáticamente.
        var entidadesDeSede = builder.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => typeof(IPerteneceASede).IsAssignableFrom(t))
            .ToList();

        foreach (var tipo in entidadesDeSede)
        {
            MetodoFiltroSede.MakeGenericMethod(tipo).Invoke(this, [builder]);
        }
    }

    private void AplicarFiltroSede<TEntidad>(ModelBuilder builder)
        where TEntidad : class, IPerteneceASede =>
        builder.Entity<TEntidad>().HasQueryFilter(e => SinRestriccionDeSede || e.SedeId == SedeActualId);
}
