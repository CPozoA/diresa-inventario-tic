using Inventario.Domain.Common;
using Inventario.Domain.Licencias;

namespace Inventario.Domain.Tests;

public sealed class LicenciaTests
{
    private static readonly Guid Sede = Guid.CreateVersion7();
    private static readonly DateOnly Hoy = new(2026, 10, 29);

    private static readonly DatosLicencia Datos = new(
        "  Microsoft 365 Empresa Estándar ", " Grupo Tecnológico Wari ", 2, new DateOnly(2026, 3, 1), new DateOnly(2027, 2, 28), "oc-2026-0145", 4_250.5m, null);

    [Fact]
    public void Registrar_LimpiaLosTextos_YEmpiezaSinEquipos()
    {
        var l = Licencia.Registrar(Sede, Datos);

        Assert.Equal("Microsoft 365 Empresa Estándar", l.Producto);
        Assert.Equal("Grupo Tecnológico Wari", l.Proveedor);
        Assert.Equal("OC-2026-0145", l.DocumentoCompra);
        Assert.Empty(l.Equipos);
        Assert.Equal(2, l.PuestosLibres);
        Assert.False(l.Archivada);
    }

    [Fact]
    public void ValidaPuestosFechasCostoYProducto()
    {
        Assert.Throws<DominioException>(() => Licencia.Registrar(Sede, Datos with { Puestos = 0 }));
        Assert.Throws<DominioException>(() => Licencia.Registrar(Sede, Datos with { FechaFin = Datos.FechaInicio }));
        Assert.Throws<DominioException>(() => Licencia.Registrar(Sede, Datos with { Costo = -1m }));
        Assert.Throws<DominioException>(() => Licencia.Registrar(Sede, Datos with { Costo = 1.005m }));
        Assert.Throws<DominioException>(() => Licencia.Registrar(Sede, Datos with { Producto = " " }));
        Assert.Throws<DominioException>(() => Licencia.Registrar(Guid.Empty, Datos));
    }

    [Fact]
    public void Asignar_OcupaPuestos_HastaElLimite_SinRepetir()
    {
        var l = Licencia.Registrar(Sede, Datos);
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();

        l.Asignar(a, Hoy);
        Assert.Throws<DominioException>(() => l.Asignar(a, Hoy));
        l.Asignar(b, Hoy);

        Assert.Equal(0, l.PuestosLibres);
        Assert.Equal(Hoy, l.Equipos.First().AsignadoEn);
        Assert.Throws<DominioException>(() => l.Asignar(Guid.CreateVersion7(), Hoy));

        l.Quitar(a);
        Assert.Equal(1, l.PuestosLibres);
        Assert.Throws<DominioException>(() => l.Quitar(a));
    }

    [Fact]
    public void NoSeAsignaSiVencio_NiSiEstaArchivada()
    {
        var vencida = Licencia.Registrar(Sede, Datos with { FechaFin = Hoy.AddDays(-1) });
        Assert.Throws<DominioException>(() => vencida.Asignar(Guid.CreateVersion7(), Hoy));

        // El último día todavía rige.
        Licencia.Registrar(Sede, Datos with { FechaFin = Hoy }).Asignar(Guid.CreateVersion7(), Hoy);

        var archivada = Licencia.Registrar(Sede, Datos);
        archivada.Archivar();
        Assert.Throws<DominioException>(() => archivada.Asignar(Guid.CreateVersion7(), Hoy));
    }

    [Fact]
    public void ConMenosPuestosQueEquipos_QuedaExcedida()
    {
        var l = Licencia.Registrar(Sede, Datos);
        l.Asignar(Guid.CreateVersion7(), Hoy);
        l.Asignar(Guid.CreateVersion7(), Hoy);

        l.Actualizar(Datos with { Puestos = 1 });

        Assert.Equal(2, l.Equipos.Count);
        Assert.Equal(0, l.PuestosLibres);
    }

    [Fact]
    public void Archivar_LiberaLosEquipos_YSePuedeReactivar()
    {
        var l = Licencia.Registrar(Sede, Datos);
        l.Asignar(Guid.CreateVersion7(), Hoy);

        l.Archivar();
        Assert.True(l.Archivada);
        Assert.Empty(l.Equipos);
        Assert.Throws<DominioException>(l.Archivar);

        l.Reactivar();
        Assert.False(l.Archivada);
        Assert.Throws<DominioException>(l.Reactivar);
    }
}
