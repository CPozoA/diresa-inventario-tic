using Inventario.Domain.Catalogos;
using Inventario.Domain.Common;

namespace Inventario.Domain.Tests;

public sealed class FichaTecnicaTests
{
    [Theory]
    [InlineData("Laptop", FichaTecnica.Computo)]
    [InlineData("All-in-one", FichaTecnica.Computo)]
    [InlineData("pc de escritorio", FichaTecnica.Computo)]
    [InlineData("Impresora", FichaTecnica.Red)]
    [InlineData("Escáner", FichaTecnica.Red)]
    [InlineData("Teléfono IP", FichaTecnica.Red)]
    [InlineData("Monitor", FichaTecnica.Ninguna)]
    [InlineData("Plotter", FichaTecnica.Ninguna)]
    public void UnTipoNuevoTomaLaFichaQueLeCorrespondePorSuNombre(string nombre, FichaTecnica esperada) =>
        Assert.Equal(esperada, TipoActivo.Crear(nombre).Ficha);

    [Fact]
    public void LosTiposInicialesDeComputoYRedTienenSuFicha()
    {
        var tipos = CatalogosIniciales.TiposActivo.Select(TipoActivo.Crear).ToList();

        Assert.Equal(FichaTecnica.Computo, tipos.Single(t => t.Nombre == "Servidor").Ficha);
        Assert.Equal(FichaTecnica.Red, tipos.Single(t => t.Nombre == "Switch").Ficha);
        Assert.Equal(FichaTecnica.Ninguna, tipos.Single(t => t.Nombre == "UPS").Ficha);
    }

    [Fact]
    public void CambiarFicha_YCompletarNoLaSobrescribe()
    {
        var tipo = TipoActivo.Crear("Monitor");

        tipo.CambiarFicha(FichaTecnica.Red);

        Assert.Equal(FichaTecnica.Red, tipo.Ficha);
        Assert.False(tipo.CompletarFichaSiFalta());
        Assert.Equal(FichaTecnica.Red, tipo.Ficha);
    }

    [Fact]
    public void CambiarFicha_ConUnValorQueNoExiste_Rechaza() =>
        Assert.Throws<DominioException>(() => TipoActivo.Crear("Monitor").CambiarFicha((FichaTecnica)99));
}
