using System.Net;
using System.Net.Http.Json;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Roles.Listar;

namespace Inventario.Api.IntegrationTests;

/// <summary>Gestión de sedes: exclusiva del superadministrador.</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class GestionSedesTests(FabricaApi api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Código de sede único (máximo 20 caracteres).</summary>
    private static string CodigoUnico() => $"P-{Guid.NewGuid():N}"[..14];

    [Fact]
    public async Task SuperAdmin_CreaUnaSede_ConLosRolesIniciales()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        var respuesta = await superAdmin.PostAsJsonAsync("/api/sedes", new { codigo = CodigoUnico(), nombre = "Sede de prueba" }, Ct);
        var creada = await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.NotNull(creada);

        var roles = await superAdmin.GetFromJsonAsync<List<RolDto>>($"/api/roles?sedeId={creada.Id}", Ct);
        Assert.NotNull(roles);
        Assert.Contains(roles, r => r.Nombre == "Editor");
        Assert.Contains(roles, r => r.Nombre == "Consulta");
    }

    [Fact]
    public async Task CodigoDeSedeRepetido_Responde409()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        var respuesta = await superAdmin.PostAsJsonAsync(
            "/api/sedes",
            new { codigo = DatosPrueba.CodigoHuanta, nombre = "Otra Huanta" },
            Ct);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("Sedes.CodigoDuplicado", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task AdministradorDeSede_NoPuedeCrearSedes()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PostAsJsonAsync("/api/sedes", new { codigo = CodigoUnico(), nombre = "No" }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task SedeDesactivada_SusUsuariosNoPuedenIngresar()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        var crear = await superAdmin.PostAsJsonAsync("/api/sedes", new { codigo = CodigoUnico(), nombre = "Sede temporal" }, Ct);
        var sede = await crear.Content.ReadFromJsonAsync<IdCreado>(Ct);
        Assert.NotNull(sede);

        var roles = await superAdmin.GetFromJsonAsync<List<RolDto>>($"/api/roles?sedeId={sede.Id}", Ct);
        var consulta = Assert.Single(roles!, r => r.Nombre == "Consulta");
        var usuario = await superAdmin.CrearUsuarioAsync(consulta.Id, sedeId: sede.Id);

        var desactivar = await superAdmin.PostAsync($"/api/sedes/{sede.Id}/desactivar", content: null, Ct);
        var ingreso = await api.IntentarIniciarSesionAsync(usuario.NombreUsuario, usuario.ContrasenaTemporal);

        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ingreso.StatusCode);
        Assert.Equal("Cuenta.SedeInactiva", await ingreso.LeerCodigoAsync());
    }
}
