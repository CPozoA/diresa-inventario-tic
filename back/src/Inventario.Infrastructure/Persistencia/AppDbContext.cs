using Inventario.Application.Common.Persistencia;
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
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<CuentaUsuario, Guid>(options), IAppDbContext, IDataProtectionKeyContext
{
    public DbSet<Sede> Sedes => Set<Sede>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Rol> Roles => Set<Rol>();

    /// <summary>Claves que cifran la cookie de sesión. Se guardan aquí para sobrevivir a los reciclajes de IIS.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

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
    }
}
