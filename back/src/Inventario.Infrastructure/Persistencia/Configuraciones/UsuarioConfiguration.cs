using Inventario.Domain.Sedes;
using Inventario.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios", Esquemas.Seguridad);

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Nombres).HasMaxLength(Usuario.LongitudMaximaNombres).IsRequired();
        builder.Property(u => u.Apellidos).HasMaxLength(Usuario.LongitudMaximaApellidos).IsRequired();
        builder.Property(u => u.Cargo).HasMaxLength(Usuario.LongitudMaximaCargo);

        builder.Ignore(u => u.NombreCompleto);
        builder.Ignore(u => u.EsSuperAdmin);

        // La sede no se borra mientras tenga usuarios.
        builder.HasOne<Sede>()
            .WithMany()
            .HasForeignKey(u => u.SedeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Roles)
            .WithOne()
            .HasForeignKey(r => r.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
