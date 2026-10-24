using Inventario.Domain.Catalogos;
using Inventario.Domain.Common;

namespace Inventario.Domain.Tests;

public sealed class CatalogoTests
{
    [Fact]
    public void Crear_QuitaEspaciosYNaceActivo()
    {
        var marca = Marca.Crear("  Dell  ");

        Assert.Equal("Dell", marca.Nombre);
        Assert.True(marca.Activo);
    }

    [Fact]
    public void Crear_SinNombreOConNombreMuyLargo_Rechaza()
    {
        Assert.Throws<DominioException>(() => TipoActivo.Crear("  "));
        Assert.Throws<DominioException>(() => TipoActivo.Crear(new string('x', ElementoCatalogo.LongitudMaximaNombre + 1)));
    }

    [Fact]
    public void CatalogoDeSede_ExigeSede() =>
        Assert.Throws<DominioException>(() => Area.Crear(Guid.Empty, "Logística"));

    [Fact]
    public void Desactivar_YVolverAActivar()
    {
        var area = Area.Crear(Guid.CreateVersion7(), "Logística");

        area.Desactivar();
        Assert.False(area.Activo);

        area.Activar();
        Assert.True(area.Activo);
    }

    [Fact]
    public void CatalogosIniciales_NoTienenNombresRepetidos()
    {
        foreach (var lista in new[]
                 {
                     CatalogosIniciales.TiposActivo, CatalogosIniciales.Marcas, CatalogosIniciales.EstadosActivo,
                     CatalogosIniciales.SistemasOperativos, CatalogosIniciales.TiposLicencia,
                 })
        {
            Assert.Equal(lista.Count, lista.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
