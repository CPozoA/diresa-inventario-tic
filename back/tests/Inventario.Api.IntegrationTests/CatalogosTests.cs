using System.Net;
using System.Net.Http.Json;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Catalogos;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.IntegrationTests;

/// <summary>
/// Catálogos: los de sede (áreas) quedan aislados por sede y los administra la sede;
/// los generales los consulta cualquiera y solo los modifica el superadministrador.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class CatalogosTests(FabricaApi api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NombreUnico(string prefijo) => $"{prefijo} {Guid.NewGuid().ToString("N")[..12]}";

    [Fact]
    public async Task LosCatalogosGeneralesArrancanConSusValoresIniciales()
    {
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var tipos = await editor.GetFromJsonAsync<List<ElementoCatalogoDto>>("/api/catalogos/tipos-activo", Ct);

        Assert.NotNull(tipos);
        Assert.Contains(tipos, t => t.Nombre == "Laptop");
        Assert.True(tipos.Count >= CatalogosIniciales.TiposActivo.Count);
    }

    [Fact]
    public async Task ListarTipos_IndicaQuePuedeGestionarCadaUno()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var tipos = await admin.GetFromJsonAsync<List<TipoCatalogoDto>>("/api/catalogos", Ct);

        Assert.NotNull(tipos);
        Assert.True(tipos.Single(t => t.Codigo == "areas").PuedeGestionar);
        Assert.False(tipos.Single(t => t.Codigo == "marcas").PuedeGestionar);
    }

    [Fact]
    public async Task Admin_CreaUnAreaQueSoloVeSuSede()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        using var puquio = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);
        var nombre = NombreUnico("Área");

        var crear = await admin.PostAsJsonAsync("/api/catalogos/areas", new { nombre }, Ct);
        var areasHuanta = await admin.GetFromJsonAsync<List<ElementoCatalogoDto>>("/api/catalogos/areas", Ct);
        var areasPuquio = await puquio.GetFromJsonAsync<List<ElementoCatalogoDto>>("/api/catalogos/areas", Ct);

        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        Assert.Contains(areasHuanta!, a => a.Nombre == nombre);
        Assert.DoesNotContain(areasPuquio!, a => a.Nombre == nombre);

        await using var db = FabricaApi.CrearContexto(UsuarioFalso.ProcesoInterno());
        var guardada = await db.Areas.SingleAsync(a => a.Nombre == nombre, Ct);
        Assert.Equal(api.SedeHuantaId, guardada.SedeId);
    }

    [Fact]
    public async Task NombreRepetidoEnLaMismaSede_Responde409_PeroEnOtraSedeSePermite()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var nombre = NombreUnico("Almacén");

        await admin.PostAsJsonAsync("/api/catalogos/areas", new { nombre }, Ct);
        var repetida = await admin.PostAsJsonAsync("/api/catalogos/areas", new { nombre = $"  {nombre} " }, Ct);
        var enPuquio = await superAdmin.PostAsJsonAsync("/api/catalogos/areas", new { sedeId = api.SedePuquioId, nombre }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal("Catalogos.NombreDuplicado", await repetida.LeerCodigoAsync());
        Assert.Equal(HttpStatusCode.Created, enPuquio.StatusCode);
    }

    [Fact]
    public async Task Admin_NoPuedeCrearEnOtraSede()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PostAsJsonAsync(
            "/api/catalogos/areas",
            new { sedeId = api.SedePuquioId, nombre = NombreUnico("Área") },
            Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Gestion.OtraSede", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Admin_NoPuedeRenombrarUnAreaDeOtraSede()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var crear = await superAdmin.PostAsJsonAsync(
            "/api/catalogos/areas",
            new { sedeId = api.SedePuquioId, nombre = NombreUnico("Área") },
            Ct);
        var area = await crear.Content.ReadFromJsonAsync<IdCreado>(Ct);

        var respuesta = await admin.PutAsJsonAsync($"/api/catalogos/areas/{area!.Id}", new { nombre = "Tomada" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Editor_SinPermisoDeCatalogos_NoPuedeCrear()
    {
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var respuesta = await editor.PostAsJsonAsync("/api/catalogos/areas", new { nombre = NombreUnico("Área") }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Catalogos.SinPermiso", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task SoloElSuperAdminModificaLosCatalogosGenerales()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var nombre = NombreUnico("Marca");

        var delAdmin = await admin.PostAsJsonAsync("/api/catalogos/marcas", new { nombre }, Ct);
        var delSuper = await superAdmin.PostAsJsonAsync("/api/catalogos/marcas", new { nombre }, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, delAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.Created, delSuper.StatusCode);
    }

    [Fact]
    public async Task UnElementoDesactivadoNoSeOfrecePeroSeConserva()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var crear = await superAdmin.PostAsJsonAsync("/api/catalogos/sistemas-operativos", new { nombre = NombreUnico("SO") }, Ct);
        var creado = await crear.Content.ReadFromJsonAsync<IdCreado>(Ct);

        var desactivar = await superAdmin.PostAsync($"/api/catalogos/sistemas-operativos/{creado!.Id}/desactivar", content: null, Ct);
        var activos = await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>("/api/catalogos/sistemas-operativos", Ct);
        var todos = await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>(
            "/api/catalogos/sistemas-operativos?incluirInactivos=true",
            Ct);

        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);
        Assert.DoesNotContain(activos!, e => e.Id == creado.Id);
        Assert.Contains(todos!, e => e.Id == creado.Id && !e.Activo);
    }

    [Fact]
    public async Task ElCatalogoDeUbicacionesSeRetiro_Responde404()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.GetAsync("/api/catalogos/ubicaciones", Ct);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnCatalogoQueNoExiste_Responde404()
    {
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var respuesta = await editor.GetAsync("/api/catalogos/colores", Ct);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("Catalogos.TipoNoExiste", await respuesta.LeerCodigoAsync());
    }
}
