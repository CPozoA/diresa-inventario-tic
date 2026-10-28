using Inventario.Domain.Activos;
using Inventario.Domain.Asignaciones;
using Inventario.Domain.Personal;
using Inventario.Domain.Sedes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

/// <summary>Entregas y devoluciones de los activos (D51).</summary>
internal sealed class AsignacionConfiguration : IEntityTypeConfiguration<Asignacion>
{
    public void Configure(EntityTypeBuilder<Asignacion> builder)
    {
        builder.ToTable("Asignaciones", Esquemas.Inventario);

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.ConservacionEntrega).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.ConservacionDevolucion).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Observaciones).HasMaxLength(Asignacion.LongitudMaximaObservaciones);
        builder.Ignore(a => a.Vigente);

        // Un activo tiene a lo sumo una asignación vigente: la base de datos lo garantiza aunque dos personas
        // asignen el mismo equipo a la vez.
        builder.HasIndex(a => a.ActivoId)
            .IsUnique()
            .HasFilter("[FechaDevolucion] IS NULL")
            .HasDatabaseName("UX_Asignaciones_ActivoVigente");
        builder.HasIndex(a => new { a.ActivoId, a.FechaEntrega });
        builder.HasIndex(a => new { a.PersonaId, a.FechaDevolucion });
        builder.HasIndex(a => new { a.SedeId, a.FechaEntrega });
        builder.HasIndex(a => new { a.SedeId, a.FechaDevolucion });

        // El historial se borra con su activo (solo se eliminan activos registrados por error).
        builder.HasOne<Activo>().WithMany().HasForeignKey(a => a.ActivoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Persona>().WithMany().HasForeignKey(a => a.PersonaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sede>().WithMany().HasForeignKey(a => a.SedeId).OnDelete(DeleteBehavior.Restrict);
    }
}
