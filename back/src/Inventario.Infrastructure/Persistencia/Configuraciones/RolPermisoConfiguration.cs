using Inventario.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> builder)
    {
        builder.ToTable("RolPermisos", Esquemas.Seguridad);

        builder.HasKey(p => new { p.RolId, p.CodigoPermiso });

        builder.Property(p => p.CodigoPermiso).HasMaxLength(100).IsRequired();
    }
}
