using Inventario.Domain.Common;
using Inventario.Domain.Mantenimientos;

namespace Inventario.Domain.Tests;

public sealed class MantenimientoTests
{
    private static readonly Guid Sede = Guid.CreateVersion7();
    private static readonly Guid Activo = Guid.CreateVersion7();
    private static readonly DateOnly Hoy = new(2026, 10, 28);
    private static readonly DatosEjecutor Interno = new(EjecutorMantenimiento.PersonalTi, Guid.CreateVersion7(), "ignorado", "x", 10m);
    private static readonly DatosEjecutor Externo = new(EjecutorMantenimiento.Proveedor, null, "  Servicios Técnicos Wari S.A.C. ", "os-2026-118", 350m);

    private static Mantenimiento Correctivo(DatosEjecutor? ejecutor = null, DateOnly? inicio = null) =>
        Mantenimiento.Abrir(Sede, Activo, TipoMantenimiento.Correctivo, inicio ?? new DateOnly(2026, 10, 20), "No enciende", ejecutor ?? Externo, Hoy);

    [Fact]
    public void Abrir_QuedaAbierto_ConLosDatosDelProveedor()
    {
        var m = Correctivo();

        Assert.True(m.Abierto);
        Assert.Equal("Servicios Técnicos Wari S.A.C.", m.Proveedor);
        Assert.Equal("OS-2026-118", m.OrdenServicio);
        Assert.Equal(350m, m.Costo);
        Assert.Null(m.TecnicoId);
    }

    [Fact]
    public void ConPersonalDeTi_SoloGuardaElTecnico()
    {
        var m = Correctivo(Interno);

        Assert.Equal(Interno.TecnicoId, m.TecnicoId);
        Assert.Null(m.Proveedor);
        Assert.Null(m.Costo);
        Assert.Throws<DominioException>(() => Correctivo(Interno with { TecnicoId = null }));
    }

    [Fact]
    public void ElCorrectivoExigeElProblema_ElPreventivoNo()
    {
        Assert.Throws<DominioException>(() =>
            Mantenimiento.Abrir(Sede, Activo, TipoMantenimiento.Correctivo, Hoy, "  ", Externo, Hoy));

        var preventivo = Mantenimiento.Abrir(Sede, Activo, TipoMantenimiento.Preventivo, Hoy, null, Interno, Hoy);
        Assert.Null(preventivo.Detalle);
    }

    [Fact]
    public void NoAceptaFechasFuturasNiProveedorSinEmpresaNiCostosInvalidos()
    {
        Assert.Throws<DominioException>(() => Correctivo(inicio: Hoy.AddDays(1)));
        Assert.Throws<DominioException>(() => Correctivo(Externo with { Proveedor = " " }));
        Assert.Throws<DominioException>(() => Correctivo(Externo with { Costo = -1m }));
        Assert.Throws<DominioException>(() => Correctivo(Externo with { Costo = 10.555m }));
    }

    [Fact]
    public void Cerrar_GuardaElResultado_YValidaFechasYCosto()
    {
        var m = Correctivo();

        Assert.Throws<DominioException>(() => m.Cerrar(new DatosCierre(new DateOnly(2026, 10, 19), "Cambio de fuente", ResultadoMantenimiento.Operativo, null), Hoy));
        Assert.Throws<DominioException>(() => m.Cerrar(new DatosCierre(Hoy.AddDays(1), "Cambio de fuente", ResultadoMantenimiento.Operativo, null), Hoy));
        Assert.Throws<DominioException>(() => m.Cerrar(new DatosCierre(Hoy, " ", ResultadoMantenimiento.Operativo, null), Hoy));

        m.Cerrar(new DatosCierre(Hoy, "Cambio de fuente de poder", ResultadoMantenimiento.Operativo, 380m), Hoy);

        Assert.False(m.Abierto);
        Assert.Equal(380m, m.Costo);
        Assert.Equal(ResultadoMantenimiento.Operativo, m.Resultado);
        Assert.Throws<DominioException>(() => m.Cerrar(new DatosCierre(Hoy, "Otra vez", ResultadoMantenimiento.Operativo, null), Hoy));
    }

    [Fact]
    public void ElCostoAlCerrar_SoloSiLoHizoUnProveedor()
    {
        var m = Correctivo(Interno);

        Assert.Throws<DominioException>(() => m.Cerrar(new DatosCierre(Hoy, "Limpieza", ResultadoMantenimiento.Operativo, 50m), Hoy));
    }
}
