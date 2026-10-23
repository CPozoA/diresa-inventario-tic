using Inventario.Domain.Common;
using Inventario.Domain.Sedes;

namespace Inventario.Domain.Tests;

public sealed class SedeTests
{
    [Fact]
    public void Crear_NormalizaElCodigoAMayusculasSinEspacios()
    {
        var sede = Sede.Crear("  san-luis ", "Red de Salud San Luis");

        Assert.Equal("SAN-LUIS", sede.Codigo);
        Assert.True(sede.Activa);
    }

    [Theory]
    [InlineData("Huánta")]
    [InlineData("SAN LUIS")]
    [InlineData("X")]
    [InlineData("")]
    public void Crear_ConCodigoInvalido_Rechaza(string codigo) =>
        Assert.Throws<DominioException>(() => Sede.Crear(codigo, "Red de Salud"));

    [Fact]
    public void Crear_SinNombre_Rechaza() =>
        Assert.Throws<DominioException>(() => Sede.Crear("HUANTA", "   "));

    [Fact]
    public void SedesIniciales_SonLasSieteRedesConCodigosUnicos()
    {
        var sedes = SedesIniciales.Crear();

        Assert.Equal(7, sedes.Count);
        Assert.Equal(7, sedes.Select(s => s.Codigo).Distinct().Count());
        Assert.Equal(7, sedes.Select(s => s.Id).Distinct().Count());
        Assert.Contains(sedes, s => s.Codigo == "HUANTA" && s.Nombre == "Red de Salud Huanta");
    }

    [Fact]
    public void SedesIniciales_TienenIdentificadoresFijosEntreEjecuciones() =>
        Assert.Equal(
            SedesIniciales.Crear().Select(s => s.Id),
            SedesIniciales.Crear().Select(s => s.Id));
}
