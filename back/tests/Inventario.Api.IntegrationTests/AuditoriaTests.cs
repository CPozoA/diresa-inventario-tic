using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Auditoria;
using Inventario.Application.Catalogos;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Auditoria;

namespace Inventario.Api.IntegrationTests;

/// <summary>Registro de auditoría (D56, §5.16).</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class AuditoriaTests(FabricaApi api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int Numero() => Random.Shared.Next(10_000_000, 99_999_999);

    private async Task<(Guid Id, string Codigo)> CrearActivoAsync(HttpClient editor)
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        async Task<Guid> Crear(string url, object cuerpo)
        {
            var respuesta = await superAdmin.PostAsJsonAsync(url, cuerpo, Ct);
            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
        }

        async Task<Guid> Elemento(string catalogo, string nombre) =>
            (await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>($"/api/catalogos/{catalogo}", Json, Ct))!
                .Single(e => e.Nombre == nombre).Id;

        var codigo = $"AUD-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var ficha = new
        {
            establecimientoId = await Crear("/api/establecimientos", new { sedeId = api.SedeHuantaId, idEstablecimiento = Numero(), codigoUnico = Numero(), nombre = "C.S. Auditoría" }),
            generales = new
            {
                codigoPatrimonial = codigo,
                tipoActivoId = await Elemento("tipos-activo", "Monitor"),
                marcaId = await Elemento("marcas", "Dell"),
                modelo = "P2422H",
                areaId = await Crear("/api/catalogos/areas", new { sedeId = api.SedeHuantaId, nombre = $"Área {Guid.NewGuid():N}"[..20] }),
                estadoActivoId = await Elemento("estados-activo", "Operativo"),
                conservacion = "Bueno",
            },
            discos = Array.Empty<object>(),
        };

        var creado = await editor.PostAsJsonAsync("/api/activos", ficha, Ct);
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var id = (await creado.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;

        var editado = await editor.PutAsJsonAsync($"/api/activos/{id}", ficha, Ct);
        Assert.Equal(HttpStatusCode.NoContent, editado.StatusCode);
        return (id, codigo);
    }

    [Fact]
    public async Task CadaCambio_QuedaRegistrado_ConQuienYEnSuRed()
    {
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var (id, codigo) = await CrearActivoAsync(editor);

        var pagina = (await admin.GetFromJsonAsync<PaginaAuditoriaDto>($"/api/auditoria?buscar={codigo}", Json, Ct))!;
        Assert.Equal(2, pagina.Total);
        Assert.Equal($"Editó el activo {codigo}", pagina.Registros[0].Accion);
        Assert.Equal($"Registró el activo {codigo}", pagina.Registros[1].Accion);
        Assert.All(pagina.Registros, r =>
        {
            Assert.Equal(ModuloAuditoria.Activos, r.Modulo);
            Assert.Equal(id, r.ActivoId);
            Assert.False(string.IsNullOrEmpty(r.Usuario));
        });

        var historial = (await admin.GetFromJsonAsync<List<RegistroAuditoriaDto>>($"/api/activos/{id}/historial", Json, Ct))!;
        Assert.Equal(2, historial.Count);

        // Otra red no lo ve; sin Auditoria.Ver no se consulta.
        var dePuquio = (await superAdmin.GetFromJsonAsync<PaginaAuditoriaDto>($"/api/auditoria?buscar={codigo}&sedeId={api.SedePuquioId}", Json, Ct))!;
        Assert.Equal(0, dePuquio.Total);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/auditoria", Ct)).StatusCode);

        // Filtros de fecha: hoy sí, mañana no.
        var hoy = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var manana = DateOnly.FromDateTime(DateTime.Now).AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(2, (await admin.GetFromJsonAsync<PaginaAuditoriaDto>($"/api/auditoria?buscar={codigo}&desde={hoy}&hasta={hoy}", Json, Ct))!.Total);
        Assert.Equal(0, (await admin.GetFromJsonAsync<PaginaAuditoriaDto>($"/api/auditoria?buscar={codigo}&desde={manana}", Json, Ct))!.Total);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/auditoria?desde={manana}&hasta={hoy}", Ct)).StatusCode);

        // Excel con lo filtrado.
        var excel = await superAdmin.GetAsync($"/api/auditoria/exportar?buscar={codigo}", Ct);
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        using var libro = new XLWorkbook(await excel.Content.ReadAsStreamAsync(Ct));
        Assert.Equal(3, libro.Worksheet("Auditoría").RowsUsed().Count());
    }

    [Fact]
    public async Task InicioDeSesionFallido_QuedaRegistrado()
    {
        var inexistente = $"no.existe.{Guid.NewGuid():N}"[..24];
        using var anonimo = api.CrearCliente();
        var intento = await anonimo.PostAsJsonAsync("/api/cuenta/iniciar-sesion", new { usuario = inexistente, contrasena = "Cualquiera-1" }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, intento.StatusCode);

        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var pagina = (await superAdmin.GetFromJsonAsync<PaginaAuditoriaDto>($"/api/auditoria?modulo=Cuenta&buscar={inexistente}", Json, Ct))!;
        var registro = Assert.Single(pagina.Registros);
        Assert.Null(registro.UsuarioId);
        Assert.Contains("que no existe", registro.Accion, StringComparison.Ordinal);

        // Los inicios de sesión correctos también quedan.
        var propios = (await superAdmin.GetFromJsonAsync<PaginaAuditoriaDto>($"/api/auditoria?modulo=Cuenta&buscar={Uri.EscapeDataString("Inició sesión")}", Json, Ct))!;
        Assert.True(propios.Total >= 1);
    }
}
