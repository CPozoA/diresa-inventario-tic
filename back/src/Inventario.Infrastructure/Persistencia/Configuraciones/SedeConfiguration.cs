using Inventario.Domain.Sedes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class SedeConfiguration : IEntityTypeConfiguration<Sede>
{
    public void Configure(EntityTypeBuilder<Sede> builder)
    {
        builder.ToTable("Sedes", Esquemas.Organizacion);

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Codigo).HasMaxLength(Sede.LongitudMaximaCodigo).IsRequired();
        builder.Property(s => s.Nombre).HasMaxLength(Sede.LongitudMaximaNombre).IsRequired();

        builder.HasIndex(s => s.Codigo).IsUnique();
    }
}
