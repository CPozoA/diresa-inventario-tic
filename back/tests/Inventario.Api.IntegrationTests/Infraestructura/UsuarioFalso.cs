using Inventario.Application.Common.Seguridad;

namespace Inventario.Api.IntegrationTests.Infraestructura;

/// <summary>Usuario actual simulado, para probar el filtro y la protección de sede directamente sobre EF Core.</summary>
public sealed record UsuarioFalso(
    bool EstaAutenticado,
    Guid? UsuarioId,
    Guid? SedeId,
    bool EsSuperAdmin,
    bool EsProcesoInterno) : IUsuarioActual
{
    public static UsuarioFalso DeSede(Guid sedeId) => new(true, Guid.CreateVersion7(), sedeId, false, false);

    public static UsuarioFalso SuperAdministrador() => new(true, Guid.CreateVersion7(), null, true, false);

    public static UsuarioFalso Anonimo() => new(false, null, null, false, false);

    public static UsuarioFalso ProcesoInterno() => new(false, null, null, false, true);
}
