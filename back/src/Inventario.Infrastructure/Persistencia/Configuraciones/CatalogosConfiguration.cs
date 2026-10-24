using Inventario.Domain.Catalogos;
using Inventario.Domain.Sedes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

/// <summary>Configuración común de los catálogos: una tabla por catálogo en el esquema <c>catalogos</c>.</summary>
internal static class ConfiguracionCatalogo
{
    /// <summary>Catálogo general: nombre único en todo el sistema.</summary>
    public static void General<T>(EntityTypeBuilder<T> builder, string tabla)
        where T : ElementoCatalogo
    {
        Comun(builder, tabla);
        builder.HasIndex(e => e.Nombre).IsUnique();
    }

    /// <summary>Catálogo de sede: nombre único dentro de cada sede.</summary>
    public static void DeSede<T>(EntityTypeBuilder<T> builder, string tabla)
        where T : ElementoCatalogoDeSede
    {
        Comun(builder, tabla);
        builder.HasIndex(e => new { e.SedeId, e.Nombre }).IsUnique();

        // La sede no se borra mientras tenga catálogos.
        builder.HasOne<Sede>()
            .WithMany()
            .HasForeignKey(e => e.SedeId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void Comun<T>(EntityTypeBuilder<T> builder, string tabla)
        where T : ElementoCatalogo
    {
        builder.ToTable(tabla, Esquemas.Catalogos);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Nombre).HasMaxLength(ElementoCatalogo.LongitudMaximaNombre).IsRequired();
    }
}

internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder) => ConfiguracionCatalogo.DeSede(builder, "Areas");
}

internal sealed class UbicacionConfiguration : IEntityTypeConfiguration<Ubicacion>
{
    public void Configure(EntityTypeBuilder<Ubicacion> builder) => ConfiguracionCatalogo.DeSede(builder, "Ubicaciones");
}

internal sealed class TipoActivoConfiguration : IEntityTypeConfiguration<TipoActivo>
{
    public void Configure(EntityTypeBuilder<TipoActivo> builder) => ConfiguracionCatalogo.General(builder, "TiposActivo");
}

internal sealed class MarcaConfiguration : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> builder) => ConfiguracionCatalogo.General(builder, "Marcas");
}

internal sealed class EstadoActivoConfiguration : IEntityTypeConfiguration<EstadoActivo>
{
    public void Configure(EntityTypeBuilder<EstadoActivo> builder) => ConfiguracionCatalogo.General(builder, "EstadosActivo");
}

internal sealed class SistemaOperativoConfiguration : IEntityTypeConfiguration<SistemaOperativo>
{
    public void Configure(EntityTypeBuilder<SistemaOperativo> builder) =>
        ConfiguracionCatalogo.General(builder, "SistemasOperativos");
}

internal sealed class TipoLicenciaConfiguration : IEntityTypeConfiguration<TipoLicencia>
{
    public void Configure(EntityTypeBuilder<TipoLicencia> builder) => ConfiguracionCatalogo.General(builder, "TiposLicencia");
}
