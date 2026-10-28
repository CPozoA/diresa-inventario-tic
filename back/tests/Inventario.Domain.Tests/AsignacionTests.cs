using Inventario.Domain.Activos;
using Inventario.Domain.Asignaciones;
using Inventario.Domain.Common;

namespace Inventario.Domain.Tests;

public sealed class AsignacionTests
{
    private static readonly Guid Sede = Guid.CreateVersion7();
    private static readonly Guid Activo = Guid.CreateVersion7();
    private static readonly Guid Persona = Guid.CreateVersion7();
    private static readonly DateOnly Hoy = new(2026, 10, 28);

    private static Asignacion Entregar(DateOnly? fecha = null) =>
        Asignacion.Entregar(Sede, Activo, Persona, fecha ?? new DateOnly(2026, 10, 1), Conservacion.Bueno, Hoy);

    [Fact]
    public void Entregar_QuedaVigente()
    {
        var asignacion = Entregar();

        Assert.True(asignacion.Vigente);
        Assert.Equal(Conservacion.Bueno, asignacion.ConservacionEntrega);
        Assert.Null(asignacion.FechaDevolucion);
    }

    [Fact]
    public void Entregar_NoAceptaFechasFuturasNiDatosVacios()
    {
        Assert.Throws<DominioException>(() => Entregar(Hoy.AddDays(1)));
        Assert.Throws<DominioException>(() => Asignacion.Entregar(Sede, Activo, Guid.Empty, Hoy, Conservacion.Bueno, Hoy));
        Assert.Throws<DominioException>(() => Asignacion.Entregar(Sede, Activo, Persona, Hoy, (Conservacion)9, Hoy));
    }

    [Fact]
    public void Devolver_CierraLaAsignacionConSuFechaYConservacion()
    {
        var asignacion = Entregar();

        asignacion.Devolver(Hoy, Conservacion.Regular, "  Se devuelve sin cargador  ", Hoy);

        Assert.False(asignacion.Vigente);
        Assert.Equal(Hoy, asignacion.FechaDevolucion);
        Assert.Equal(Conservacion.Regular, asignacion.ConservacionDevolucion);
        Assert.Equal("Se devuelve sin cargador", asignacion.Observaciones);
    }

    [Fact]
    public void Devolver_ValidaLasFechasYNoSeDevuelveDosVeces()
    {
        var asignacion = Entregar(new DateOnly(2026, 10, 10));

        Assert.Throws<DominioException>(() => asignacion.Devolver(new DateOnly(2026, 10, 9), Conservacion.Bueno, null, Hoy));
        Assert.Throws<DominioException>(() => asignacion.Devolver(Hoy.AddDays(1), Conservacion.Bueno, null, Hoy));

        // El mismo día de la entrega sí (reasignación inmediata).
        asignacion.Devolver(new DateOnly(2026, 10, 10), Conservacion.Bueno, null, Hoy);
        Assert.Throws<DominioException>(() => asignacion.Devolver(Hoy, Conservacion.Bueno, null, Hoy));
    }
}
