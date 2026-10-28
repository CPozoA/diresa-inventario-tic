using Inventario.Domain.Catalogos;
using Inventario.Domain.Establecimientos;
using Inventario.Domain.Personal;
using Inventario.Domain.Sedes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

/// <summary>Personal de cada red (D50): a quién se le asignan los equipos.</summary>
internal sealed class PersonaConfiguration : IEntityTypeConfiguration<Persona>
{
    /// <summary>Sin distinguir mayúsculas ni tildes: "PEREZ" encuentra a "PÉREZ".</summary>
    private const string IntercalacionBusqueda = "Modern_Spanish_CI_AI";

    public void Configure(EntityTypeBuilder<Persona> builder)
    {
        builder.ToTable("Personal", Esquemas.Organizacion);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Dni).HasMaxLength(Persona.LongitudDni).IsUnicode(false).IsFixedLength();
        builder.Property(p => p.ApellidoPaterno).HasMaxLength(Persona.LongitudMaximaApellido);
        builder.Property(p => p.ApellidoMaterno).HasMaxLength(Persona.LongitudMaximaApellido);
        builder.Property(p => p.Nombres).HasMaxLength(Persona.LongitudMaximaNombres);
        builder.Property(p => p.NombreCompleto).HasMaxLength(Persona.LongitudMaximaNombreCompleto).UseCollation(IntercalacionBusqueda);
        builder.Property(p => p.Cargo).HasMaxLength(Persona.LongitudMaximaCargo);
        builder.Ignore(p => p.Verificada);

        // El DNI no se repite dentro de la red (la misma persona puede estar en otra red si se trasladó).
        builder.HasIndex(p => new { p.SedeId, p.Dni }).IsUnique();
        builder.HasIndex(p => new { p.SedeId, p.Activa, p.ApellidoPaterno });

        builder.HasOne<Sede>().WithMany().HasForeignKey(p => p.SedeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Establecimiento>().WithMany().HasForeignKey(p => p.EstablecimientoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Area>().WithMany().HasForeignKey(p => p.AreaId).OnDelete(DeleteBehavior.Restrict);
    }
}
