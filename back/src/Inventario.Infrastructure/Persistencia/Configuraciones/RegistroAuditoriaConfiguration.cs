using Inventario.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Registro de auditoría (D56). Sin claves foráneas: debe sobrevivir a lo que describe (un activo eliminado, una sede
/// que cambia) y no frenar ningún borrado.
/// </summary>
internal sealed class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("Auditoria", Esquemas.Seguridad);

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Modulo).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Accion).HasMaxLength(RegistroAuditoria.LongitudMaximaAccion).IsRequired();
        builder.Property(r => r.Ip).HasMaxLength(RegistroAuditoria.LongitudMaximaIp);

        builder.HasIndex(r => r.Fecha);
        builder.HasIndex(r => new { r.SedeId, r.Fecha });
        builder.HasIndex(r => new { r.ActivoId, r.Fecha });
        builder.HasIndex(r => new { r.UsuarioId, r.Fecha });
    }
}
