using Inventario.Domain.Catalogos;
using Inventario.Domain.Common;

namespace Inventario.Domain.Tests;

public sealed class EstadoActivoTests
{
    [Theory]
    [InlineData("Operativo", GrupoEstado.EnUso)]
    [InlineData("En uso", GrupoEstado.EnUso)]
    [InlineData("En mantenimiento", GrupoEstado.EnMantenimiento)]
    [InlineData("En reparación", GrupoEstado.EnMantenimiento)]
    [InlineData("Inoperativo", GrupoEstado.FueraDeServicio)]
    [InlineData("Malogrado", GrupoEstado.FueraDeServicio)]
    [InlineData("En almacén", GrupoEstado.EnAlmacen)]
    [InlineData("De baja", GrupoEstado.DeBaja)]
    [InlineData("Dado de baja", GrupoEstado.DeBaja)]
    [InlineData("Prestado", GrupoEstado.EnUso)]
    public void UnEstadoNuevoTomaSuGrupoPorElNombre(string nombre, GrupoEstado esperado) =>
        Assert.Equal(esperado, EstadoActivo.Crear(nombre).Grupo);

    [Fact]
    public void ElGrupoSePuedeCambiar_PeroNoAUnValorInexistente()
    {
        var estado = EstadoActivo.Crear("Prestado");

        estado.CambiarGrupo(GrupoEstado.EnAlmacen);

        Assert.Equal(GrupoEstado.EnAlmacen, estado.Grupo);
        Assert.Throws<DominioException>(() => estado.CambiarGrupo((GrupoEstado)99));
    }

    [Fact]
    public void UnEstadoQueYaTieneGrupoNoSeCompleta()
    {
        var estado = EstadoActivo.Crear("Inoperativo");

        Assert.False(estado.CompletarGrupoSiFalta());
        Assert.Equal(GrupoEstado.FueraDeServicio, estado.Grupo);
    }
}
