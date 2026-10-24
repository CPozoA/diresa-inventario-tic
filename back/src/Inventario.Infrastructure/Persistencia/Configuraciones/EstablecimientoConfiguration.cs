using Inventario.Domain.Establecimientos;
using Inventario.Domain.Sedes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class EstablecimientoConfiguration : IEntityTypeConfiguration<Establecimiento>
{
    public void Configure(EntityTypeBuilder<Establecimiento> builder)
    {
        builder.ToTable("Establecimientos", Esquemas.Organizacion);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Números de la lista oficial: los asigna quien registra, no la base de datos.
        builder.Property(e => e.IdEstablecimiento).ValueGeneratedNever();
        builder.Property(e => e.CodigoUnico).ValueGeneratedNever();

        builder.Property(e => e.Nombre).HasMaxLength(Establecimiento.LongitudMaximaNombre).IsRequired();
        builder.Property(e => e.Categoria).HasMaxLength(Establecimiento.LongitudMaximaCategoria);
        builder.Property(e => e.Microred).HasMaxLength(Establecimiento.LongitudMaximaMicrored);
        builder.Property(e => e.Provincia).HasMaxLength(Establecimiento.LongitudMaximaProvincia);
        builder.Property(e => e.Distrito).HasMaxLength(Establecimiento.LongitudMaximaDistrito);
        builder.Property(e => e.Ubigeo).HasMaxLength(Establecimiento.LongitudUbigeo);

        // Únicos en todo el sistema (no solo en la red).
        builder.HasIndex(e => e.IdEstablecimiento).IsUnique();
        builder.HasIndex(e => e.CodigoUnico).IsUnique();

        // Listados de una red ordenados por nombre.
        builder.HasIndex(e => new { e.SedeId, e.Nombre });

        // La sede no se borra mientras tenga establecimientos.
        builder.HasOne<Sede>()
            .WithMany()
            .HasForeignKey(e => e.SedeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
