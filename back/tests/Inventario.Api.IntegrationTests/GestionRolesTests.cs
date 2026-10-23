using System.Net;
using System.Net.Http.Json;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Roles.Listar;
using Inventario.Application.Roles.ListarPermisos;
using Inventario.Domain.Seguridad;

namespace Inventario.Api.IntegrationTests;

/// <summary>Gestión de roles de sede: solo se otorgan permisos que uno ya tiene (docs/ARQUITECTURA.md §6.3).</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class GestionRolesTests(FabricaApi api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_CreaEditaYEliminaUnRolDeSuSede()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var nombre = AyudasHttp.NombreUnico("rol");

        var crear = await admin.PostAsJsonAsync(
            "/api/roles",
            new { nombre, descripcion = "Rol de prueba", permisos = new[] { Permisos.Activos.Ver } },
            Ct);
        var creado = await crear.Content.ReadFromJsonAsync<IdCreado>(Ct);
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        Assert.NotNull(creado);

        var editar = await admin.PutAsJsonAsync(
            $"/api/roles/{creado.Id}",
            new { nombre, permisos = new[] { Permisos.Activos.Ver, Permisos.Licencias.Ver } },
            Ct);
        Assert.Equal(HttpStatusCode.NoContent, editar.StatusCode);

        var roles = await admin.GetFromJsonAsync<List<RolDto>>("/api/roles", Ct);
        var rol = Assert.Single(roles!, r => r.Id == creado.Id);
        Assert.Equal(new[] { Permisos.Activos.Ver, Permisos.Licencias.Ver }, rol.Permisos);

        var eliminar = await admin.DeleteAsync($"/api/roles/{creado.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, eliminar.StatusCode);
    }

    [Fact]
    public async Task Admin_VeLosRolesDeSuSedeYElDeAdministrador_PeroNoLosDeOtraSede()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var roles = await admin.GetFromJsonAsync<List<RolDto>>("/api/roles", Ct);

        Assert.NotNull(roles);
        Assert.Contains(roles, r => r.Id == RolesDelSistema.AdministradorSedeId);
        Assert.Contains(roles, r => r.Id == api.RolTecnicoHuantaId);
        Assert.DoesNotContain(roles, r => r.Id == api.RolTecnicoPuquioId);
        Assert.DoesNotContain(roles, r => r.Id == RolesDelSistema.SuperAdminId);
    }

    [Fact]
    public async Task Admin_NoPuedeDarAUnRolUnPermisoExclusivoDelSuperAdmin()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PostAsJsonAsync(
            "/api/roles",
            new { nombre = AyudasHttp.NombreUnico("rol"), permisos = new[] { Permisos.Sedes.Gestionar } },
            Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Gestion.PermisosQueNoTiene", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Admin_EditarUnRolDeOtraSede_Responde404()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PutAsJsonAsync(
            $"/api/roles/{api.RolTecnicoPuquioId}",
            new { nombre = "Tomado", permisos = new[] { Permisos.Activos.Ver } },
            Ct);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Admin_EditarElRolDeAdministrador_Responde422()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PutAsJsonAsync(
            $"/api/roles/{RolesDelSistema.AdministradorSedeId}",
            new { nombre = "Administrador", permisos = Array.Empty<string>() },
            Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("Roles.DelSistema", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task EliminarUnRolAsignado_Responde409()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var roles = await admin.GetFromJsonAsync<List<RolDto>>("/api/roles", Ct);
        var editor = Assert.Single(roles!, r => r.Nombre == "Editor");

        var respuesta = await admin.DeleteAsync($"/api/roles/{editor.Id}", Ct);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("Roles.EnUso", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Gestor_NoPuedeCrearUnRolConPermisosQueNoTiene()
    {
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);

        var respuesta = await gestor.PostAsJsonAsync(
            "/api/roles",
            new { nombre = AyudasHttp.NombreUnico("rol"), permisos = new[] { Permisos.Activos.Ver, Permisos.Activos.Eliminar } },
            Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Gestion.PermisosQueNoTiene", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Gestor_NoPuedeModificarUnRolConMasPermisosQueEl()
    {
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);
        var roles = await gestor.GetFromJsonAsync<List<RolDto>>("/api/roles", Ct);
        var editor = Assert.Single(roles!, r => r.Nombre == "Editor");

        // Aunque solo quisiera quitarle permisos: el rol Editor tiene permisos que el gestor no tiene.
        var respuesta = await gestor.PutAsJsonAsync(
            $"/api/roles/{editor.Id}",
            new { nombre = "Editor", permisos = new[] { Permisos.Activos.Ver } },
            Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Roles.ConMasPermisos", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Gestor_VeQuePermisosPuedeOtorgar()
    {
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);

        var permisos = await gestor.GetFromJsonAsync<List<PermisoDto>>("/api/roles/permisos", Ct);

        Assert.NotNull(permisos);
        Assert.True(permisos.Single(p => p.Codigo == Permisos.Activos.Ver).PuedeOtorgar);
        Assert.False(permisos.Single(p => p.Codigo == Permisos.Activos.Eliminar).PuedeOtorgar);
        Assert.DoesNotContain(permisos, p => p.Codigo == Permisos.Sedes.Gestionar);
    }
}
