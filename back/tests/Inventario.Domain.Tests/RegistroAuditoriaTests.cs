using Inventario.Domain.Auditoria;
using Inventario.Domain.Common;

namespace Inventario.Domain.Tests;

public sealed class RegistroAuditoriaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 30, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Crear_GuardaLaAccion_YRecortaLoQueExcede()
    {
        var r = RegistroAuditoria.Crear(Ahora, Guid.CreateVersion7(), null, ModuloAuditoria.Activos, "  Registró el activo PAT-0001 ", null, "192.168.1.20");
        Assert.Equal("Registró el activo PAT-0001", r.Accion);
        Assert.Equal("192.168.1.20", r.Ip);

        var larga = RegistroAuditoria.Crear(Ahora, null, null, ModuloAuditoria.Cuenta, new string('x', 900), null, new string('9', 60));
        Assert.Equal(RegistroAuditoria.LongitudMaximaAccion, larga.Accion.Length);
        Assert.EndsWith("…", larga.Accion, StringComparison.Ordinal);
        Assert.Equal(RegistroAuditoria.LongitudMaximaIp, larga.Ip!.Length);
    }

    [Fact]
    public void Crear_ExigeLaAccion()
    {
        Assert.Throws<DominioException>(() => RegistroAuditoria.Crear(Ahora, null, null, ModuloAuditoria.Activos, "  ", null, null));
    }
}
