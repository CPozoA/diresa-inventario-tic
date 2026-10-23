using System.Net;
using System.Net.Http.Json;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Cuenta;
using Inventario.Application.Roles.Listar;
using Inventario.Application.Usuarios;
using Inventario.Application.Usuarios.RestablecerContrasena;
using Inventario.Domain.Seguridad;

namespace Inventario.Api.IntegrationTests;

/// <summary>
/// Gestión de usuarios a través de la API, con las reglas contra el escalamiento de privilegios
/// (docs/ARQUITECTURA.md §6.3): nadie se gestiona a sí mismo, nadie otorga permisos que no tiene,
/// nadie gestiona a quien tiene más permisos que él y solo el superadministrador gestiona administradores.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class GestionUsuariosTests(FabricaApi api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_ListaSoloUsuariosDeSuSede()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var usuarios = await admin.GetFromJsonAsync<List<UsuarioDto>>("/api/usuarios", Ct);

        Assert.NotNull(usuarios);
        Assert.All(usuarios, u => Assert.Equal(api.SedeHuantaId, u.SedeId));
        Assert.Contains(usuarios, u => u.NombreUsuario == DatosPrueba.EditorHuanta);
        Assert.DoesNotContain(usuarios, u => u.NombreUsuario == DatosPrueba.EditorPuquio);
        Assert.DoesNotContain(usuarios, u => u.NombreUsuario == DatosPrueba.SuperAdmin);
    }

    [Fact]
    public async Task Admin_CreaUsuarioEnSuSede_ConContrasenaTemporalQueDebeCambiar()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var nuevo = await admin.CrearUsuarioAsync(api.RolTecnicoHuantaId);

        var detalle = await admin.GetFromJsonAsync<UsuarioDto>($"/api/usuarios/{nuevo.Id}", Ct);
        Assert.NotNull(detalle);
        Assert.Equal(api.SedeHuantaId, detalle.SedeId);
        Assert.True(detalle.DebeCambiarContrasena);
        Assert.Contains(detalle.Roles, r => r.Id == api.RolTecnicoHuantaId);

        using var usuario = await api.CrearClienteConSesionAsync(nuevo.NombreUsuario, nuevo.ContrasenaTemporal);
        var sesion = await usuario.GetFromJsonAsync<SesionDto>("/api/cuenta/sesion", Ct);
        Assert.True(sesion?.DebeCambiarContrasena);
    }

    [Fact]
    public async Task Admin_CrearUsuarioEnOtraSede_Responde403()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PedirCrearUsuarioAsync(api.RolTecnicoPuquioId, sedeId: api.SedePuquioId);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Gestion.OtraSede", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Admin_AsignarRolDeOtraSede_SeRechaza()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        // Para el administrador de Huanta, el rol de Puquio "no existe".
        var respuesta = await admin.PedirCrearUsuarioAsync(api.RolTecnicoPuquioId);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("Usuarios.RolNoEncontrado", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Admin_NoPuedeCrearOtroAdministrador()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PedirCrearUsuarioAsync(RolesDelSistema.AdministradorSedeId);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Gestion.SoloSuperAdminGestionaAdministradores", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task SuperAdmin_CreaUnAdministrador_YOtroAdministradorNoPuedeGestionarlo()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var nuevoAdmin = await superAdmin.CrearUsuarioAsync(RolesDelSistema.AdministradorSedeId, sedeId: api.SedeHuantaId);

        var respuesta = await admin.PostAsync($"/api/usuarios/{nuevoAdmin.Id}/desactivar", content: null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Gestion.SoloSuperAdminGestionaAdministradores", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Admin_NoPuedeCambiarSusPropiosRoles()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var adminId = await api.ObtenerUsuarioIdAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PutAsJsonAsync(
            $"/api/usuarios/{adminId}/roles",
            new { rolIds = new[] { api.RolTecnicoHuantaId } },
            Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Gestion.NoASiMismo", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Admin_RestablecerContrasenaDeUsuarioDeOtraSede_Responde404()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var editorPuquio = await api.ObtenerUsuarioIdAsync(DatosPrueba.EditorPuquio);

        var respuesta = await admin.PostAsync($"/api/usuarios/{editorPuquio}/restablecer-contrasena", content: null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Editor_SinPermisoDeUsuarios_Responde403()
    {
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var listar = await editor.GetAsync("/api/usuarios", Ct);
        var crear = await editor.PedirCrearUsuarioAsync(api.RolTecnicoHuantaId);

        Assert.Equal(HttpStatusCode.Forbidden, listar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
    }

    [Fact]
    public async Task Gestor_CreaUsuarioConUnRolCuyosPermisosTiene()
    {
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);

        // "Técnico de pruebas" solo da Activos.Ver, que el gestor tiene.
        var respuesta = await gestor.PedirCrearUsuarioAsync(api.RolTecnicoHuantaId);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task Gestor_NoPuedeAsignarUnRolConPermisosQueNoTiene()
    {
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);
        var roles = await gestor.GetFromJsonAsync<List<RolDto>>("/api/roles", Ct);
        var editor = Assert.Single(roles!, r => r.Nombre == "Editor");

        var respuesta = await gestor.PedirCrearUsuarioAsync(editor.Id);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Gestion.PermisosQueNoTiene", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task Gestor_NoPuedeRestablecerLaContrasenaDeQuienTieneMasPermisos()
    {
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);
        var editorHuanta = await api.ObtenerUsuarioIdAsync(DatosPrueba.EditorHuanta);

        var respuesta = await gestor.PostAsync($"/api/usuarios/{editorHuanta}/restablecer-contrasena", content: null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal("Gestion.ObjetivoConMasPermisos", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task NombreDeUsuarioRepetido_Responde409()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PedirCrearUsuarioAsync(api.RolTecnicoHuantaId, nombreUsuario: DatosPrueba.EditorHuanta);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("Usuarios.NombreUsuarioDuplicado", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task DatoQueIncumpleElDominio_Responde400ConSuMensaje()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var respuesta = await admin.PedirCrearUsuarioAsync(api.RolTecnicoHuantaId, nombres: "   ");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("Dominio.ReglaIncumplida", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task UsuarioDesactivado_NoPuedeIniciarSesion()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var nuevo = await admin.CrearUsuarioAsync(api.RolTecnicoHuantaId);

        var desactivar = await admin.PostAsync($"/api/usuarios/{nuevo.Id}/desactivar", content: null, Ct);
        var ingreso = await api.IntentarIniciarSesionAsync(nuevo.NombreUsuario, nuevo.ContrasenaTemporal);

        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ingreso.StatusCode);
        Assert.Equal("Cuenta.Inactiva", await ingreso.LeerCodigoAsync());
    }

    [Fact]
    public async Task RestablecerContrasena_LaAnteriorDejaDeServirYLaNuevaExigeCambiarla()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var nuevo = await admin.CrearUsuarioAsync(api.RolTecnicoHuantaId);

        var respuesta = await admin.PostAsync($"/api/usuarios/{nuevo.Id}/restablecer-contrasena", content: null, Ct);
        var temporal = await respuesta.Content.ReadFromJsonAsync<ContrasenaTemporalDto>(Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(temporal);
        Assert.NotEqual(nuevo.ContrasenaTemporal, temporal.ContrasenaTemporal);

        var conLaAnterior = await api.IntentarIniciarSesionAsync(nuevo.NombreUsuario, nuevo.ContrasenaTemporal);
        Assert.Equal(HttpStatusCode.Unauthorized, conLaAnterior.StatusCode);

        var conLaNueva = await api.IntentarIniciarSesionAsync(nuevo.NombreUsuario, temporal.ContrasenaTemporal);
        var sesion = await conLaNueva.Content.ReadFromJsonAsync<SesionDto>(Ct);
        Assert.Equal(HttpStatusCode.OK, conLaNueva.StatusCode);
        Assert.True(sesion?.DebeCambiarContrasena);
    }

    [Fact]
    public async Task Desbloquear_PermiteIngresarAntesDeLos15Minutos()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var nuevo = await admin.CrearUsuarioAsync(api.RolTecnicoHuantaId);

        for (var intento = 0; intento < 3; intento++)
        {
            await api.IntentarIniciarSesionAsync(nuevo.NombreUsuario, "No-es-la-clave-1");
        }

        var bloqueado = await api.IntentarIniciarSesionAsync(nuevo.NombreUsuario, nuevo.ContrasenaTemporal);
        Assert.Equal("Cuenta.Bloqueada", await bloqueado.LeerCodigoAsync());

        var detalle = await admin.GetFromJsonAsync<UsuarioDto>($"/api/usuarios/{nuevo.Id}", Ct);
        Assert.True(detalle?.Bloqueado);

        var desbloquear = await admin.PostAsync($"/api/usuarios/{nuevo.Id}/desbloquear", content: null, Ct);
        var ingreso = await api.IntentarIniciarSesionAsync(nuevo.NombreUsuario, nuevo.ContrasenaTemporal);

        Assert.Equal(HttpStatusCode.NoContent, desbloquear.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);
    }
}
