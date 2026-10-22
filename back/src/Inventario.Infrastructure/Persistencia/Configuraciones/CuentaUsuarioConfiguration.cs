using Inventario.Domain.Usuarios;
using Inventario.Infrastructure.Identidad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// La cuenta de Identity comparte la clave primaria con <see cref="Usuario"/> (relación uno a uno).
/// Identity ya configura columnas e índices (nombre de usuario y correo únicos); aquí solo se ubica la tabla
/// y se enlaza con el usuario.
/// </summary>
internal sealed class CuentaUsuarioConfiguration : IEntityTypeConfiguration<CuentaUsuario>
{
    public void Configure(EntityTypeBuilder<CuentaUsuario> builder)
    {
        builder.ToTable("Cuentas", Esquemas.Seguridad);

        builder.HasOne<Usuario>()
            .WithOne()
            .HasForeignKey<CuentaUsuario>(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
