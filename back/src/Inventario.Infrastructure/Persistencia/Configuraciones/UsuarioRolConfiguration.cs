using Inventario.Domain.Seguridad;
using Inventario.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class UsuarioRolConfiguration : IEntityTypeConfiguration<UsuarioRol>
{
    public void Configure(EntityTypeBuilder<UsuarioRol> builder)
    {
        builder.ToTable("UsuarioRoles", Esquemas.Seguridad);

        builder.HasKey(r => new { r.UsuarioId, r.RolId });

        // Un rol asignado a usuarios no se puede borrar: primero hay que quitárselo.
        builder.HasOne<Rol>()
            .WithMany()
            .HasForeignKey(r => r.RolId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
