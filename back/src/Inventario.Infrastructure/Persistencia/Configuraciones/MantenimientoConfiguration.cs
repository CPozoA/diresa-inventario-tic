using Inventario.Domain.Activos;
using Inventario.Domain.Mantenimientos;
using Inventario.Domain.Sedes;
using Inventario.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mantenimientos de los activos (D53).</summary>
internal sealed class MantenimientoConfiguration : IEntityTypeConfiguration<Mantenimiento>
{
    public void Configure(EntityTypeBuilder<Mantenimiento> builder)
    {
        builder.ToTable("Mantenimientos", Esquemas.Inventario);

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Ejecutor).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Resultado).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Detalle).HasMaxLength(Mantenimiento.LongitudMaximaDetalle);
        builder.Property(m => m.TrabajoRealizado).HasMaxLength(Mantenimiento.LongitudMaximaDetalle);
        builder.Property(m => m.Proveedor).HasMaxLength(Mantenimiento.LongitudMaximaProveedor);
        builder.Property(m => m.OrdenServicio).HasMaxLength(Mantenimiento.LongitudMaximaOrdenServicio);
        builder.Property(m => m.Costo).HasPrecision(10, 2);
        builder.Ignore(m => m.Abierto);

        // Un activo tiene a lo sumo un mantenimiento abierto, aunque dos personas lo registren a la vez.
        builder.HasIndex(m => m.ActivoId)
            .IsUnique()
            .HasFilter("[FechaFin] IS NULL")
            .HasDatabaseName("UX_Mantenimientos_ActivoAbierto");
        builder.HasIndex(m => new { m.ActivoId, m.FechaInicio });
        builder.HasIndex(m => new { m.SedeId, m.FechaFin, m.FechaInicio });

        // El historial se borra con su activo (solo se eliminan activos registrados por error).
        builder.HasOne<Activo>().WithMany().HasForeignKey(m => m.ActivoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Sede>().WithMany().HasForeignKey(m => m.SedeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(m => m.TecnicoId).OnDelete(DeleteBehavior.Restrict);
    }
}
