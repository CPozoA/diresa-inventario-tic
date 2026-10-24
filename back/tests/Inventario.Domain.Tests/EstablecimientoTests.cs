using Inventario.Domain.Common;
using Inventario.Domain.Establecimientos;
using Inventario.Domain.Sedes;

namespace Inventario.Domain.Tests;

public sealed class EstablecimientoTests
{
    private static readonly Guid Sede = Guid.CreateVersion7();

    private static DatosEstablecimiento Datos(
        int id = 1234,
        int codigo = 5678,
        string nombre = "Puesto de Salud Luricocha",
        string? ubigeo = "050601") =>
        new(id, codigo, nombre, "I-2", "Microred Huanta", "Huanta", "Luricocha", ubigeo);

    [Fact]
    public void Crear_GuardaLosDatosSinEspaciosYNaceActivo()
    {
        var establecimiento = Establecimiento.Crear(Sede, Datos(nombre: "  Centro de Salud Huanta  ") with { Categoria = "  " });

        Assert.Equal(Sede, establecimiento.SedeId);
        Assert.Equal("Centro de Salud Huanta", establecimiento.Nombre);
        Assert.Null(establecimiento.Categoria);
        Assert.Equal("050601", establecimiento.Ubigeo);
        Assert.True(establecimiento.Activo);
    }

    [Fact]
    public void Crear_SinSede_Rechaza() =>
        Assert.Throws<DominioException>(() => Establecimiento.Crear(Guid.Empty, Datos()));

    [Theory]
    [InlineData(0, 5678, "Nombre", "050601")]
    [InlineData(1234, -1, "Nombre", "050601")]
    [InlineData(1234, 5678, "   ", "050601")]
    [InlineData(1234, 5678, "Nombre", "50601")]
    [InlineData(1234, 5678, "Nombre", "05060A")]
    public void Crear_ConDatosInvalidos_Rechaza(int id, int codigo, string nombre, string ubigeo) =>
        Assert.Throws<DominioException>(() => Establecimiento.Crear(Sede, Datos(id, codigo, nombre, ubigeo)));

    [Fact]
    public void Crear_SinUbigeo_SePermite() =>
        Assert.Null(Establecimiento.Crear(Sede, Datos(ubigeo: null)).Ubigeo);

    [Fact]
    public void Actualizar_IndicaSiAlgoCambio()
    {
        var establecimiento = Establecimiento.Crear(Sede, Datos());

        Assert.False(establecimiento.Actualizar(Datos()));
        Assert.True(establecimiento.Actualizar(Datos(nombre: "Puesto de Salud Luricocha II")));
        Assert.Equal("Puesto de Salud Luricocha II", establecimiento.Nombre);
        Assert.Equal(Sede, establecimiento.SedeId);
    }

    [Fact]
    public void Actualizar_ConDatosInvalidos_NoCambiaNada()
    {
        var establecimiento = Establecimiento.Crear(Sede, Datos());

        Assert.Throws<DominioException>(() => establecimiento.Actualizar(Datos(nombre: "Otro nombre", ubigeo: "123")));
        Assert.Equal("Puesto de Salud Luricocha", establecimiento.Nombre);
    }

    [Fact]
    public void DesactivarYActivar()
    {
        var establecimiento = Establecimiento.Crear(Sede, Datos());

        establecimiento.Desactivar();
        Assert.False(establecimiento.Activo);

        establecimiento.Activar();
        Assert.True(establecimiento.Activo);
    }

    [Theory]
    [InlineData("Red de Salud Huanta", "HUANTA")]
    [InlineData("RED DE SALUD HUANTA", "HUANTA")]
    [InlineData("huanta", "HUANTA")]
    [InlineData("Red Huamánga", "HUAMANGA")]
    [InlineData("San Francisco", "SAN-FRANCISCO")]
    [InlineData("SAN-MIGUEL", "SAN-MIGUEL")]
    [InlineData("Red de Salud Ayacucho Centro", "CENTRO")]
    [InlineData("  Coracora ", "CORACORA")]
    public void ReconocedorRed_ReconoceLasVariantesHabituales(string red, string codigoSede)
    {
        var sede = ReconocedorRed.Buscar(red, SedesIniciales.Crear());

        Assert.NotNull(sede);
        Assert.Equal(codigoSede, sede.Codigo);
    }

    [Theory]
    [InlineData("Red de Salud Lima")]
    [InlineData("Huanta y Puquio")]
    [InlineData("Red")]
    [InlineData("")]
    [InlineData("  -  ")]
    public void ReconocedorRed_NoAdivinaSiNoReconoceONoEsClaro(string red) =>
        Assert.Null(ReconocedorRed.Buscar(red, SedesIniciales.Crear()));

    [Fact]
    public void ReconocedorRed_SinTexto_NoReconoceNada() =>
        Assert.Null(ReconocedorRed.Buscar(null, SedesIniciales.Crear()));
}
