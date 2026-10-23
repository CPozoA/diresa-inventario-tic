using System.Net;
using System.Net.Http.Json;
using Inventario.Api.IntegrationTests.Infraestructura;

namespace Inventario.Api.IntegrationTests;

/// <summary>Protección contra CSRF (docs/ARQUITECTURA.md D24): todo cambio exige el token antifalsificación.</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class ProteccionCsrfTests(FabricaApi api)
{
    private const string Cabecera = "X-XSRF-TOKEN";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SinToken_UnPostSeRechazaConCodigo()
    {
        using var cliente = api.CrearClienteSinCsrf();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/cuenta/iniciar-sesion",
            new { usuario = DatosPrueba.EditorHuanta, contrasena = DatosPrueba.Contrasena },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("Seguridad.TokenCsrfInvalido", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task ConTokenEnCookieYCabecera_ElPostSeAcepta()
    {
        using var cliente = api.CrearClienteSinCsrf();
        var token = await PedirTokenAsync(cliente);

        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/cuenta/iniciar-sesion")
        {
            Content = JsonContent.Create(new { usuario = DatosPrueba.EditorHuanta, contrasena = DatosPrueba.Contrasena }),
        };
        peticion.Headers.Add(Cabecera, token);

        var respuesta = await cliente.SendAsync(peticion, Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task TokenObtenidoAntesDeIniciarSesion_NoSirveDespues()
    {
        using var cliente = api.CrearClienteSinCsrf();
        var tokenAnonimo = await PedirTokenAsync(cliente);

        using var ingreso = new HttpRequestMessage(HttpMethod.Post, "/api/cuenta/iniciar-sesion")
        {
            Content = JsonContent.Create(new { usuario = DatosPrueba.EditorPuquio, contrasena = DatosPrueba.Contrasena }),
        };
        ingreso.Headers.Add(Cabecera, tokenAnonimo);
        (await cliente.SendAsync(ingreso, Ct)).EnsureSuccessStatusCode();

        // El token está ligado al usuario: el de la etapa anónima ya no vale con la sesión iniciada.
        using var cierre = new HttpRequestMessage(HttpMethod.Post, "/api/cuenta/cerrar-sesion");
        cierre.Headers.Add(Cabecera, tokenAnonimo);
        var respuesta = await cliente.SendAsync(cierre, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("Seguridad.TokenCsrfInvalido", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task LasConsultasNoNecesitanToken()
    {
        using var cliente = api.CrearClienteSinCsrf();

        var respuesta = await cliente.GetAsync("/api/salud", Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private static async Task<string> PedirTokenAsync(HttpClient cliente)
    {
        var respuesta = await cliente.GetFromJsonAsync<TokenRespuesta>("/api/cuenta/csrf", Ct);
        return respuesta?.Token ?? throw new InvalidOperationException("La API no devolvió el token.");
    }

    private sealed record TokenRespuesta(string Token);
}
