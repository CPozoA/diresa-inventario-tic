using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Establecimientos;
using Inventario.Domain.Sedes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventario.Infrastructure.Persistencia.Configuraciones;

internal sealed class ActivoConfiguration : IEntityTypeConfiguration<Activo>
{
    public void Configure(EntityTypeBuilder<Activo> builder)
    {
        builder.ToTable("Activos", Esquemas.Inventario);

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.CodigoPatrimonial).HasMaxLength(Activo.LongitudMaximaCodigo).IsRequired();
        builder.Property(a => a.Modelo).HasMaxLength(Activo.LongitudMaximaModelo).IsRequired();
        builder.Property(a => a.NumeroSerie).HasMaxLength(Activo.LongitudMaximaSerie);
        builder.Property(a => a.Descripcion).HasMaxLength(Activo.LongitudMaximaDescripcion);
        builder.Property(a => a.UbicacionFisica).HasMaxLength(Activo.LongitudMaximaUbicacion);
        builder.Property(a => a.Responsable).HasMaxLength(Activo.LongitudMaximaResponsable);
        builder.Property(a => a.Observaciones).HasMaxLength(Activo.LongitudMaximaObservaciones);

        // Listas fijas como texto ("Bueno", "Media"…): la tabla se lee sin consultar el código.
        builder.Property(a => a.Conservacion).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Criticidad).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.TipoAdquisicion).HasConversion<string>().HasMaxLength(30);

        builder.Property(a => a.Proveedor).HasMaxLength(Activo.LongitudMaximaProveedor);
        builder.Property(a => a.NumeroOrdenCompra).HasMaxLength(Activo.LongitudMaximaOrdenCompra);
        builder.Property(a => a.ValorAdquisicion).HasPrecision(12, 2);

        builder.Property(a => a.GeneracionProcesador).HasMaxLength(GeneracionesDeProcesador.LongitudMaximaCodigo);
        builder.Property(a => a.TipoGrafica).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.ModeloGrafica).HasMaxLength(Activo.LongitudMaximaTextoTecnico);
        builder.Property(a => a.PantallaPulgadas).HasPrecision(4, 1);
        builder.Property(a => a.VersionSistemaOperativo).HasMaxLength(Activo.LongitudMaximaTextoCorto);
        builder.Property(a => a.NombreEquipo).HasMaxLength(Activo.LongitudMaximaNombreEquipo);
        builder.Property(a => a.DominioGrupo).HasMaxLength(Activo.LongitudMaximaTextoTecnico);
        builder.Property(a => a.DireccionIp).HasMaxLength(Activo.LongitudMaximaIp);
        builder.Property(a => a.DireccionMac).HasMaxLength(Activo.LongitudMac);
        // D45: listas marcadas, guardadas como nombres separados por comas ("UsbA,Hdmi").
        builder.Property(a => a.PuertosGuardados).HasColumnName("Puertos").HasMaxLength(300);
        builder.Property(a => a.AccesoriosGuardados).HasColumnName("Accesorios").HasMaxLength(300);
        builder.Ignore(a => a.Puertos);
        builder.Ignore(a => a.Accesorios);

        // Totales calculados en memoria a partir de discos y RAM.
        builder.Ignore(a => a.RamTotalGb);
        builder.Ignore(a => a.AlmacenamientoTotalGb);

        // Código patrimonial único dentro de la sede.
        builder.HasIndex(a => new { a.SedeId, a.CodigoPatrimonial }).IsUnique();

        // Búsquedas y filtros frecuentes.
        builder.HasIndex(a => new { a.SedeId, a.NumeroSerie });
        builder.HasIndex(a => a.EstablecimientoId);
        builder.HasIndex(a => a.FinGarantia);
        builder.HasIndex(a => a.FechaRenovacion);
        builder.HasIndex(a => new { a.SedeId, a.ProximoMantenimiento });

        // Las referencias no se borran mientras haya activos que las usen (los catálogos se desactivan, no se borran).
        builder.HasOne<Sede>().WithMany().HasForeignKey(a => a.SedeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Establecimiento>().WithMany().HasForeignKey(a => a.EstablecimientoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TipoActivo>().WithMany().HasForeignKey(a => a.TipoActivoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Marca>().WithMany().HasForeignKey(a => a.MarcaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Area>().WithMany().HasForeignKey(a => a.AreaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EstadoActivo>().WithMany().HasForeignKey(a => a.EstadoActivoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SistemaOperativo>().WithMany().HasForeignKey(a => a.SistemaOperativoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Procesador>().WithMany().HasForeignKey(a => a.ProcesadorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Antivirus>().WithMany().HasForeignKey(a => a.AntivirusId).OnDelete(DeleteBehavior.Restrict);

        // Discos y RAM forman parte del activo: se borran con él.
        builder.HasMany(a => a.Discos).WithOne().HasForeignKey(d => d.ActivoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Discos).HasField("_discos").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.ModulosRam).WithOne().HasForeignKey(m => m.ActivoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.ModulosRam).HasField("_modulosRam").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class DiscoActivoConfiguration : IEntityTypeConfiguration<DiscoActivo>
{
    public void Configure(EntityTypeBuilder<DiscoActivo> builder)
    {
        builder.ToTable("Discos", Esquemas.Inventario);

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Ranura).HasMaxLength(DiscoActivo.LongitudMaximaRanura);
        builder.Property(d => d.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Uso).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.MarcaModelo).HasMaxLength(DiscoActivo.LongitudMaximaMarcaModelo);
        builder.Property(d => d.NumeroSerie).HasMaxLength(DiscoActivo.LongitudMaximaSerie);
    }
}

internal sealed class ModuloRamConfiguration : IEntityTypeConfiguration<ModuloRam>
{
    public void Configure(EntityTypeBuilder<ModuloRam> builder)
    {
        builder.ToTable("ModulosRam", Esquemas.Inventario);

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Ranura).HasMaxLength(ModuloRam.LongitudMaximaRanura);
        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.MarcaModelo).HasMaxLength(ModuloRam.LongitudMaximaMarcaModelo);
    }
}
