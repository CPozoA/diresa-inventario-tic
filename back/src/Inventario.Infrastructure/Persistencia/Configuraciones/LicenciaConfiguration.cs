using Inventario.Domain.Activos;
using Inventario.Domain.Licencias;
using Inventario.Domain.Sedes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

/// <summary>Suscripciones de software de cada red (D54).</summary>
internal sealed class LicenciaConfiguration : IEntityTypeConfiguration<Licencia>
{
    public void Configure(EntityTypeBuilder<Licencia> builder)
    {
        builder.ToTable("Licencias", Esquemas.Inventario);

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Producto).HasMaxLength(Licencia.LongitudMaximaProducto).IsRequired();
        builder.Property(l => l.Proveedor).HasMaxLength(Licencia.LongitudMaximaProveedor);
        builder.Property(l => l.DocumentoCompra).HasMaxLength(Licencia.LongitudMaximaDocumento);
        builder.Property(l => l.Observaciones).HasMaxLength(Licencia.LongitudMaximaObservaciones);
        builder.Property(l => l.Costo).HasPrecision(10, 2);
        builder.Ignore(l => l.PuestosLibres);

        builder.HasIndex(l => new { l.SedeId, l.Archivada, l.FechaFin });

        builder.HasOne<Sede>().WithMany().HasForeignKey(l => l.SedeId).OnDelete(DeleteBehavior.Restrict);

        // Los equipos son parte de la licencia: se borran con ella.
        builder.HasMany(l => l.Equipos).WithOne().HasForeignKey(e => e.LicenciaId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Equipos).HasField("_equipos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class EquipoLicenciaConfiguration : IEntityTypeConfiguration<EquipoLicencia>
{
    public void Configure(EntityTypeBuilder<EquipoLicencia> builder)
    {
        builder.ToTable("EquiposLicencia", Esquemas.Inventario);

        // Un equipo ocupa a lo sumo un puesto de cada licencia, aunque dos personas lo asignen a la vez.
        builder.HasKey(e => new { e.LicenciaId, e.ActivoId });
        builder.HasIndex(e => e.ActivoId);

        // Al eliminar un activo (registrado por error) se libera su puesto.
        builder.HasOne<Activo>().WithMany().HasForeignKey(e => e.ActivoId).OnDelete(DeleteBehavior.Cascade);
    }
}
