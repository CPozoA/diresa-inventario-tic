using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Cuenta;
using Inventario.Application.Sedes.Listar;
using Inventario.Domain.Sedes;
using Inventario.Domain.Seguridad;

namespace Inventario.Api.IntegrationTests;

/// <summary>Sesión y permisos a través de la API real (HTTP, cookie, autorización).</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class AutorizacionTests(FabricaApi api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SinSesion_LaApiResponde401()
    {
        using var cliente = api.CrearCliente();

        var respuesta = await cliente.GetAsync("/api/sedes", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Salud_RespondeSinSesion()
    {
        using var cliente = api.CrearCliente();

        var respuesta = await cliente.GetAsync("/api/salud", Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task ContrasenaIncorrecta_Responde401ConCodigo()
    {
        using var cliente = api.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/cuenta/iniciar-sesion",
            new { usuario = DatosPrueba.EditorPuquio, contrasena = "No-es-la-clave-1" },
            Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal("Cuenta.CredencialesInvalidas", await LeerCodigoAsync(respuesta));
    }

    [Fact]
    public async Task UsuarioDeSede_SesionTraeSuSedeYLosPermisosDeEditor()
    {
        using var cliente = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var sesion = await cliente.GetFromJsonAsync<SesionDto>("/api/cuenta/sesion", Ct);

        Assert.NotNull(sesion);
        Assert.False(sesion.EsSuperAdmin);
        Assert.Equal(DatosPrueba.CodigoHuanta, sesion.Sede?.Codigo);
        Assert.Contains(Permisos.Activos.Crear, sesion.Permisos);
        Assert.DoesNotContain(Permisos.Sedes.Gestionar, sesion.Permisos);
        Assert.DoesNotContain(Permisos.Roles.Gestionar, sesion.Permisos);
    }

    [Fact]
    public async Task UsuarioDeSede_SinPermisoDeSedes_Responde403()
    {
        using var cliente = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var respuesta = await cliente.GetAsync("/api/sedes", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task SuperAdministrador_ListaLasSieteSedes()
    {
        using var cliente = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        var sedes = await cliente.GetFromJsonAsync<List<SedeDto>>("/api/sedes", Ct);

        // Al menos las 7 sedes iniciales (otras pruebas pueden crear sedes nuevas).
        Assert.NotNull(sedes);
        Assert.Superset(
            SedesIniciales.Crear().Select(s => s.Codigo).ToHashSet(),
            sedes.Select(s => s.Codigo).ToHashSet());
    }

    [Fact]
    public async Task ContrasenaTemporal_BloqueaLaApiHastaCambiarla()
    {
        using var cliente = await api.CrearClienteConSesionAsync(DatosPrueba.TemporalHuanta);

        var respuesta = await cliente.GetAsync("/api/sedes", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Cuenta.DebeCambiarContrasena", await LeerCodigoAsync(respuesta));
    }

    [Fact]
    public async Task CerrarSesion_LaSesionDejaDeValer()
    {
        using var cliente = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);

        var cierre = await cliente.PostAsync("/api/cuenta/cerrar-sesion", content: null, Ct);
        var despues = await cliente.GetAsync("/api/cuenta/sesion", Ct);

        Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    private static async Task<string?> LeerCodigoAsync(HttpResponseMessage respuesta)
    {
        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct));
        return documento.RootElement.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null;
    }
}
