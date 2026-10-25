using Inventario.Domain.Activos;
using Inventario.Domain.Common;

namespace Inventario.Domain.Tests;

public sealed class ActivoTests
{
    private static readonly Guid Sede = Guid.CreateVersion7();
    private static readonly Guid Establecimiento = Guid.CreateVersion7();
    private static readonly DateOnly Hoy = new(2026, 10, 24);

    private static DatosGeneralesActivo Generales(string codigo = "pat-0001", string modelo = "Latitude 5440") =>
        new(codigo, Guid.CreateVersion7(), Guid.CreateVersion7(), modelo, "7XK2P93", null, Guid.CreateVersion7(),
            "Piso 2, oficina 204", "Juan Pérez Quispe", Guid.CreateVersion7(), Conservacion.Bueno, Criticidad.Media, null);

    private static DatosAdquisicionActivo Adquisicion(DateOnly? fecha = null, int? vidaUtil = 4, DateOnly? finGarantia = null) =>
        new(fecha ?? new DateOnly(2023, 3, 15), "Proveedor Tecnológico S.A.C.", "OC-2023-0145", TipoAdquisicion.Compra, 3850m, vidaUtil, finGarantia);

    private static DatosTecnicosActivo Tecnicos(string? ip = null, string? mac = null) =>
        DatosTecnicosActivo.Vacios with { ProcesadorId = Guid.CreateVersion7(), GeneracionProcesador = "13", DireccionIp = ip, DireccionMac = mac };

    private static Activo Crear(
        DatosAdquisicionActivo? adquisicion = null,
        DatosTecnicosActivo? tecnicos = null,
        IReadOnlyList<DatosDisco>? discos = null,
        IReadOnlyList<DatosModuloRam>? modulos = null) =>
        Activo.Crear(Sede, Establecimiento, Generales(), adquisicion ?? Adquisicion(), tecnicos ?? Tecnicos(), discos ?? [], modulos ?? []);

    [Fact]
    public void Crear_NormalizaElCodigoYSumaDiscosYRam()
    {
        var activo = Crear(
            discos: [new("M.2 #1", TipoDisco.Nvme, 512, "Samsung PM9B1", null, UsoDisco.Sistema), new("M.2 #2", TipoDisco.SsdSata, 1024, null, null, UsoDisco.Datos)],
            modulos: [new("A", TipoRam.Ddr5, 8, 5600, null), new("B", TipoRam.Ddr5, 8, 5600, null)]);

        Assert.Equal("PAT-0001", activo.CodigoPatrimonial);
        Assert.Equal(Sede, activo.SedeId);
        Assert.Equal(1536, activo.AlmacenamientoTotalGb);
        Assert.Equal(16, activo.RamTotalGb);
        Assert.Equal(new[] { 0, 1 }, activo.Discos.Select(d => d.Orden));
        Assert.All(activo.Discos, d => Assert.Equal(activo.Id, d.ActivoId));
    }

    [Fact]
    public void Crear_SinCodigoOSinModelo_Rechaza()
    {
        Assert.Throws<DominioException>(() =>
            Activo.Crear(Sede, Establecimiento, Generales(codigo: "  "), Adquisicion(), Tecnicos(), [], []));
        Assert.Throws<DominioException>(() =>
            Activo.Crear(Sede, Establecimiento, Generales(modelo: ""), Adquisicion(), Tecnicos(), [], []));
    }

    [Fact]
    public void Crear_SinEstablecimiento_Rechaza() =>
        Assert.Throws<DominioException>(() => Activo.Crear(Sede, Guid.Empty, Generales(), Adquisicion(), Tecnicos(), [], []));

    [Fact]
    public void PuertosYAccesorios_SeGuardanSinRepetirYEnElOrdenDeLaLista()
    {
        var activo = Crear(tecnicos: DatosTecnicosActivo.Vacios with
        {
            Puertos = [Puerto.Rj45, Puerto.UsbA, Puerto.Hdmi, Puerto.UsbA],
            Accesorios = [Accesorio.Maletin, Accesorio.Cargador],
        });

        Assert.Equal("UsbA,Hdmi,Rj45", activo.PuertosGuardados);
        Assert.Equal(new[] { Puerto.UsbA, Puerto.Hdmi, Puerto.Rj45 }, activo.Puertos.AsEnumerable());
        Assert.Equal(new[] { Accesorio.Cargador, Accesorio.Maletin }, activo.Accesorios.AsEnumerable());
        Assert.Null(Crear().PuertosGuardados);
    }

    [Fact]
    public void ElModeloDeGraficaSoloSiEsDedicada()
    {
        var dedicada = Crear(tecnicos: DatosTecnicosActivo.Vacios with { TipoGrafica = TipoGrafica.Dedicada, ModeloGrafica = " NVIDIA T550 " });

        Assert.Equal("NVIDIA T550", dedicada.ModeloGrafica);
        Assert.Throws<DominioException>(() =>
            Crear(tecnicos: DatosTecnicosActivo.Vacios with { TipoGrafica = TipoGrafica.Integrada, ModeloGrafica = "Intel UHD" }));
    }

    [Fact]
    public void LaGeneracionSinProcesador_Rechaza() =>
        Assert.Throws<DominioException>(() =>
            Crear(tecnicos: DatosTecnicosActivo.Vacios with { GeneracionProcesador = "13" }));

    [Fact]
    public void LaGarantiaNoTerminaAntesDeLaAdquisicion() =>
        Assert.Throws<DominioException>(() => Crear(adquisicion: Adquisicion(finGarantia: new DateOnly(2022, 1, 1))));

    [Theory]
    [InlineData("192.168.10.45", true)]
    [InlineData("10", false)]
    [InlineData("300.1.1.1", false)]
    [InlineData("fe80::1", true)]
    [InlineData("hola", false)]
    public void DireccionIp_SeValida(string ip, bool valida)
    {
        if (valida)
        {
            Assert.Equal(ip, Crear(tecnicos: Tecnicos(ip: ip)).DireccionIp);
        }
        else
        {
            Assert.Throws<DominioException>(() => Crear(tecnicos: Tecnicos(ip: ip)));
        }
    }

    [Theory]
    [InlineData("a4-bb-6d-12-34-56")]
    [InlineData("A4BB6D123456")]
    [InlineData(" A4:BB:6D:12:34:56 ")]
    public void DireccionMac_SeGuardaConDosPuntosYMayusculas(string mac) =>
        Assert.Equal("A4:BB:6D:12:34:56", Crear(tecnicos: Tecnicos(mac: mac)).DireccionMac);

    [Fact]
    public void DireccionMac_Invalida_Rechaza() =>
        Assert.Throws<DominioException>(() => Crear(tecnicos: Tecnicos(mac: "A4:BB:6D:12:34")));

    [Fact]
    public void Ram_SoloAceptaCapacidadesYVelocidadesDeSuTipo()
    {
        Assert.Throws<DominioException>(() => Crear(modulos: [new("A", TipoRam.Ddr4, 7, null, null)]));
        Assert.Throws<DominioException>(() => Crear(modulos: [new("A", TipoRam.Ddr4, 8, 5600, null)]));
        Assert.Equal(8, Crear(modulos: [new("A", TipoRam.Ddr4, 8, 3200, null)]).RamTotalGb);
    }

    [Fact]
    public void Disco_ConCapacidadFueraDeRango_Rechaza()
    {
        Assert.Throws<DominioException>(() => Crear(discos: [new(null, TipoDisco.Hdd, 0, null, null, UsoDisco.Datos)]));
        Assert.Throws<DominioException>(() =>
            Crear(discos: [new(null, TipoDisco.Hdd, ListasActivo.CapacidadMaximaDiscoGb + 1, null, null, UsoDisco.Datos)]));
    }

    [Fact]
    public void Actualizar_ReemplazaDiscosYRam_YConservaLaSede()
    {
        var activo = Crear(discos: [new(null, TipoDisco.Hdd, 500, null, null, UsoDisco.Sistema)]);
        var otroEstablecimiento = Guid.CreateVersion7();

        activo.Actualizar(otroEstablecimiento, Generales(), Adquisicion(), Tecnicos(), [], [new("A", TipoRam.Ddr4, 16, null, null)]);

        Assert.Empty(activo.Discos);
        Assert.Equal(16, activo.RamTotalGb);
        Assert.Equal(otroEstablecimiento, activo.EstablecimientoId);
        Assert.Equal(Sede, activo.SedeId);
    }

    [Fact]
    public void Actualizar_ConUnDatoInvalido_NoCambiaNada()
    {
        var activo = Crear();

        Assert.Throws<DominioException>(() =>
            activo.Actualizar(Establecimiento, Generales(modelo: "Otro modelo"), Adquisicion(), Tecnicos(ip: "999.1.1.1"), [], []));
        Assert.Equal("Latitude 5440", activo.Modelo);
    }

    [Fact]
    public void Situacion_CalculaAntiguedadGarantiaYRenovacion()
    {
        // Adquirido el 15/03/2023 con 4 años de vida útil: se renueva el 15/03/2027 (a menos de un año de "hoy").
        var activo = Crear(adquisicion: Adquisicion(finGarantia: new DateOnly(2026, 3, 14)));

        var situacion = activo.Situacion(Hoy);

        Assert.Equal(new DateOnly(2027, 3, 15), activo.FechaRenovacion);
        Assert.Equal(3.6m, situacion.AntiguedadAnios);
        Assert.Equal(EstadoGarantia.Vencida, situacion.Garantia);
        Assert.Equal(0.4m, situacion.VidaUtilRestanteAnios);
        Assert.Equal(SituacionRenovacion.ProximoARenovar, situacion.Renovacion);
    }

    [Theory]
    [InlineData(2025, 1, 1, SituacionRenovacion.Vigente)]
    [InlineData(2022, 1, 1, SituacionRenovacion.Renovar)]
    public void Situacion_Renovacion(int anio, int mes, int dia, SituacionRenovacion esperada) =>
        Assert.Equal(esperada, Crear(adquisicion: Adquisicion(fecha: new DateOnly(anio, mes, dia))).Situacion(Hoy).Renovacion);

    [Fact]
    public void Situacion_SinFechasNiVidaUtil_EsSinDato()
    {
        var situacion = Crear(adquisicion: DatosAdquisicionActivo.Vacios).Situacion(Hoy);

        Assert.Null(situacion.AntiguedadAnios);
        Assert.Equal(EstadoGarantia.SinDato, situacion.Garantia);
        Assert.Equal(SituacionRenovacion.SinDato, situacion.Renovacion);
    }
}
