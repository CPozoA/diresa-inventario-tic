using Inventario.Domain.Sedes;
using Inventario.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("Roles", Esquemas.Seguridad);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Nombre).HasMaxLength(Rol.LongitudMaximaNombre).IsRequired();
        builder.Property(r => r.Descripcion).HasMaxLength(Rol.LongitudMaximaDescripcion);

        // Se guarda como texto ("SuperAdmin", "AdministradorSede", "DeSede") para que la tabla se lea sin consultar el código.
        builder.Property(r => r.Tipo).HasConversion<string>().HasMaxLength(30);

        builder.Ignore(r => r.EsDelSistema);

        // Un rol de sede no puede quedar huérfano: la sede no se borra mientras tenga roles.
        builder.HasOne<Sede>()
            .WithMany()
            .HasForeignKey(r => r.SedeId)
            .OnDelete(DeleteBehavior.Restrict);

        // No puede haber dos roles con el mismo nombre en una sede.
        builder.HasIndex(r => new { r.SedeId, r.Nombre }).IsUnique();

        builder.HasMany(r => r.PermisosAsignados)
            .WithOne()
            .HasForeignKey(p => p.RolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.PermisosAsignados)
            .HasField("_permisos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
