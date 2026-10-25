using Inventario.Domain.Catalogos;
using Inventario.Domain.Common;

namespace Inventario.Domain.Tests;

public sealed class ProcesadorTests
{
    [Theory]
    [InlineData("Intel Core i5", GeneracionesProcesador.IntelCore)]
    [InlineData("intel core i9", GeneracionesProcesador.IntelCore)]
    [InlineData("Intel Core Ultra 7", GeneracionesProcesador.IntelCoreUltra)]
    [InlineData("AMD Ryzen 5", GeneracionesProcesador.AmdRyzen)]
    [InlineData("Intel Celeron", GeneracionesProcesador.Ninguna)]
    [InlineData("Apple M2", GeneracionesProcesador.Ninguna)]
    public void UnaFamiliaNuevaTomaLasGeneracionesQueLeCorrespondenPorSuNombre(string nombre, GeneracionesProcesador esperadas) =>
        Assert.Equal(esperadas, Procesador.Crear(nombre).Generaciones);

    [Fact]
    public void CadaFamiliaOfreceSusGeneraciones()
    {
        var intel = GeneracionesDeProcesador.De(GeneracionesProcesador.IntelCore);
        var ryzen = GeneracionesDeProcesador.De(GeneracionesProcesador.AmdRyzen);

        Assert.Equal(14, intel.Count);
        Assert.Equal("13.ª generación", GeneracionesDeProcesador.Buscar(GeneracionesProcesador.IntelCore, "13")?.Texto);
        Assert.Equal("Serie 5000", GeneracionesDeProcesador.Buscar(GeneracionesProcesador.AmdRyzen, "5000")?.Texto);
        Assert.Equal(9, ryzen.Count);
        Assert.Empty(GeneracionesDeProcesador.De(GeneracionesProcesador.Ninguna));
    }

    [Fact]
    public void UnaGeneracionDeOtraFamiliaNoSeEncuentra()
    {
        Assert.Null(GeneracionesDeProcesador.Buscar(GeneracionesProcesador.IntelCore, "5000"));
        Assert.Null(GeneracionesDeProcesador.Buscar(GeneracionesProcesador.Ninguna, "13"));
        Assert.Null(GeneracionesDeProcesador.Buscar(GeneracionesProcesador.IntelCore, null));
    }

    [Fact]
    public void ElSuperadministradorCambiaLasGeneraciones()
    {
        var procesador = Procesador.Crear("Intel Xeon");
        procesador.CambiarGeneraciones(GeneracionesProcesador.IntelCore);

        Assert.Equal(GeneracionesProcesador.IntelCore, procesador.Generaciones);
        Assert.Throws<DominioException>(() => procesador.CambiarGeneraciones((GeneracionesProcesador)99));
    }

    [Fact]
    public void LosProcesadoresInicialesTienenSusGeneraciones()
    {
        var iniciales = CatalogosIniciales.Procesadores.Select(Procesador.Crear).ToList();

        Assert.Equal(GeneracionesProcesador.IntelCore, iniciales.Single(p => p.Nombre == "Intel Core i3").Generaciones);
        Assert.Equal(GeneracionesProcesador.AmdRyzen, iniciales.Single(p => p.Nombre == "AMD Ryzen 9").Generaciones);
        Assert.Equal(GeneracionesProcesador.Ninguna, iniciales.Single(p => p.Nombre == "Otro").Generaciones);
    }
}
