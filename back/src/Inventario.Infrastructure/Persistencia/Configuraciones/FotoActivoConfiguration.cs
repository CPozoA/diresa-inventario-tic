using Inventario.Domain.Activos;
using Inventario.Domain.Sedes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

/// <summary>Fotos de los activos (D38): los datos; los archivos están en el almacén de archivos.</summary>
internal sealed class FotoActivoConfiguration : IEntityTypeConfiguration<FotoActivo>
{
    public void Configure(EntityTypeBuilder<FotoActivo> builder)
    {
        builder.ToTable("FotosActivo", Esquemas.Inventario);

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Descripcion).HasMaxLength(FotoActivo.LongitudMaximaDescripcion);
        builder.Property(f => f.Tipo).HasConversion<string>().HasMaxLength(10);
        builder.Property(f => f.TipoMiniatura).HasConversion<string>().HasMaxLength(10);
        builder.Ignore(f => f.Clave);
        builder.Ignore(f => f.ClaveMiniatura);

        builder.HasIndex(f => new { f.ActivoId, f.Orden });

        // Las fotos se borran con su activo (sus archivos los borra quien elimina el activo).
        builder.HasOne<Activo>().WithMany().HasForeignKey(f => f.ActivoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Sede>().WithMany().HasForeignKey(f => f.SedeId).OnDelete(DeleteBehavior.Restrict);
    }
}
