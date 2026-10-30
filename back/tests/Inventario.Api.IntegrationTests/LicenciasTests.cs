using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Catalogos;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Licencias;
using Inventario.Domain.Catalogos;

namespace Inventario.Api.IntegrationTests;

/// <summary>Licencias de software (D54, §5.14).</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class LicenciasTests(FabricaApi api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    private static int Numero() => Random.Shared.Next(10_000_000, 99_999_999);

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record Referencias(Guid Establecimiento, Guid Area, Guid Tipo, Guid Marca, Guid Operativo, Guid DeBaja);

    private async Task<Referencias> PrepararAsync()
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

        return new Referencias(
            await Crear("/api/establecimientos", new { sedeId = api.SedeHuantaId, idEstablecimiento = Numero(), codigoUnico = Numero(), nombre = "C.S. Licencias" }),
            await Crear("/api/catalogos/areas", new { sedeId = api.SedeHuantaId, nombre = $"Área {Guid.NewGuid():N}"[..20] }),
            await Elemento("tipos-activo", "Monitor"),
            await Elemento("marcas", "Dell"),
            await Elemento("estados-activo", "Operativo"),
            await Elemento("estados-activo", "De baja"));
    }

    private static async Task<(Guid Id, string Codigo)> CrearActivoAsync(HttpClient cliente, Referencias r, Guid? estado = null)
    {
        var codigo = $"LIC-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/activos",
            new
            {
                establecimientoId = r.Establecimiento,
                generales = new
                {
                    codigoPatrimonial = codigo,
                    tipoActivoId = r.Tipo,
                    marcaId = r.Marca,
                    modelo = "Latitude 5440",
                    areaId = r.Area,
                    estadoActivoId = estado ?? r.Operativo,
                    conservacion = "Bueno",
                },
                discos = Array.Empty<object>(),
            },
            Ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return ((await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id, codigo);
    }

    private static object Datos(string producto, int puestos, DateOnly inicio, DateOnly fin, Guid? sedeId = null) => new
    {
        sedeId,
        producto,
        proveedor = "Grupo Tecnológico Wari",
        puestos,
        fechaInicio = Fecha(inicio),
        fechaFin = Fecha(fin),
        documentoCompra = "oc-2026-0145",
        costo = 4250.50m,
        observaciones = (string?)null,
    };

    private static async Task<Guid> RegistrarAsync(HttpClient cliente, object datos)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/licencias", datos, Ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
    }

    private static Task<HttpResponseMessage> AsignarAsync(HttpClient cliente, Guid licencia, params Guid[] activos) =>
        cliente.PostAsJsonAsync($"/api/licencias/{licencia}/equipos", new { activoIds = activos }, Ct);

    private static Task<FichaLicenciaDto?> FichaAsync(HttpClient cliente, Guid licencia) =>
        cliente.GetFromJsonAsync<FichaLicenciaDto>($"/api/licencias/{licencia}", Json, Ct);

    [Fact]
    public async Task Asignar_OcupaPuestos_YAparecenEnLaFichaDelActivo_YSeQuitan()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var producto = $"Microsoft 365 {Guid.NewGuid():N}"[..30];
        var licencia = await RegistrarAsync(editor, Datos(producto, 2, Hoy.AddMonths(-2), Hoy.AddMonths(10)));
        var (a, codigoA) = await CrearActivoAsync(editor, r);
        var (b, _) = await CrearActivoAsync(editor, r);
        var (c, _) = await CrearActivoAsync(editor, r);

        Assert.Equal(HttpStatusCode.NoContent, (await AsignarAsync(editor, licencia, a, b)).StatusCode);

        var ficha = (await FichaAsync(editor, licencia))!;
        Assert.Equal("OC-2026-0145", ficha.DocumentoCompra);
        Assert.Equal(VigenciaLicencia.Vigente, ficha.Vigencia);
        Assert.Equal(2, ficha.Equipos.Count);
        Assert.Equal(GrupoEstado.EnUso, ficha.Equipos[0].GrupoEstado);

        var lleno = await AsignarAsync(editor, licencia, c);
        Assert.Equal("Licencias.SinPuestos", await lleno.LeerCodigoAsync());
        Assert.Equal("Licencias.YaAsignada", await (await AsignarAsync(editor, licencia, a)).LeerCodigoAsync());

        var delActivo = await editor.GetFromJsonAsync<List<LicenciaDelActivoDto>>($"/api/activos/{a}/licencias", Json, Ct);
        Assert.Equal(licencia, Assert.Single(delActivo!).LicenciaId);

        var porEquipo = await editor.GetFromJsonAsync<List<LicenciaDto>>($"/api/licencias?buscar={codigoA}", Json, Ct);
        Assert.Equal(2, Assert.Single(porEquipo!).Equipos);

        Assert.Equal(HttpStatusCode.NoContent, (await editor.DeleteAsync($"/api/licencias/{licencia}/equipos/{a}", Ct)).StatusCode);
        Assert.Empty((await editor.GetFromJsonAsync<List<LicenciaDelActivoDto>>($"/api/activos/{a}/licencias", Json, Ct))!);
        Assert.Equal(HttpStatusCode.NoContent, (await AsignarAsync(editor, licencia, c)).StatusCode);
    }

    [Fact]
    public async Task Alertas_PorVencerVencidasYExcedidas_YArchivarLasQuita()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var marca = Guid.NewGuid().ToString("N")[..8];
        var porVencer = await RegistrarAsync(editor, Datos($"ESET {marca}", 5, Hoy.AddMonths(-11), Hoy.AddDays(10)));
        var vencida = await RegistrarAsync(editor, Datos($"Adobe {marca}", 5, Hoy.AddYears(-1), Hoy.AddDays(-3)));
        var excedida = await RegistrarAsync(editor, Datos($"Office {marca}", 2, Hoy.AddMonths(-1), Hoy.AddYears(1)));

        var antes = (await editor.GetFromJsonAsync<CifrasLicenciasDto>("/api/licencias/cifras", Json, Ct))!;

        var (a, _) = await CrearActivoAsync(editor, r);
        var (b, _) = await CrearActivoAsync(editor, r);
        Assert.Equal(HttpStatusCode.NoContent, (await AsignarAsync(editor, excedida, a, b)).StatusCode);

        // Al renovar con menos puestos queda excedida.
        var editar = await editor.PutAsJsonAsync($"/api/licencias/{excedida}", Datos($"Office {marca}", 1, Hoy, Hoy.AddYears(1)), Ct);
        Assert.Equal(HttpStatusCode.NoContent, editar.StatusCode);

        var lista = (await editor.GetFromJsonAsync<List<LicenciaDto>>($"/api/licencias?buscar={marca}", Json, Ct))!;
        Assert.Equal(VigenciaLicencia.PorVencer, lista.Single(l => l.Id == porVencer).Vigencia);
        Assert.Equal(10, lista.Single(l => l.Id == porVencer).DiasRestantes);
        Assert.Equal(VigenciaLicencia.Vencida, lista.Single(l => l.Id == vencida).Vigencia);
        Assert.True(lista.Single(l => l.Id == excedida).Excedida);
        Assert.Equal(excedida, Assert.Single((await editor.GetFromJsonAsync<List<LicenciaDto>>($"/api/licencias?filtro=Excedidas&buscar={marca}", Json, Ct))!).Id);

        var cifras = (await editor.GetFromJsonAsync<CifrasLicenciasDto>("/api/licencias/cifras", Json, Ct))!;
        Assert.True(cifras.Excedidas >= 1);
        Assert.True(cifras.Vencidas >= 1);
        Assert.True(cifras.PorVencer >= 1);

        // Una vencida no admite equipos hasta renovarla; archivada deja de contar.
        Assert.Equal("Licencias.Vencida", await (await AsignarAsync(editor, vencida, a)).LeerCodigoAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await editor.PostAsync($"/api/licencias/{vencida}/archivar", null, Ct)).StatusCode);
        var despues = (await editor.GetFromJsonAsync<CifrasLicenciasDto>("/api/licencias/cifras", Json, Ct))!;
        Assert.Equal(cifras.Vencidas - 1, despues.Vencidas);
        Assert.Equal(antes.Vencidas - 1, despues.Vencidas);
        Assert.DoesNotContain((await editor.GetFromJsonAsync<List<LicenciaDto>>($"/api/licencias?buscar={marca}", Json, Ct))!, l => l.Id == vencida);
        Assert.Equal(VigenciaLicencia.Archivada, (await FichaAsync(editor, vencida))!.Vigencia);
        Assert.Equal("Licencias.Archivada", await (await AsignarAsync(editor, vencida, a)).LeerCodigoAsync());
    }

    [Fact]
    public async Task Reglas_OtraRed_EquiposDeBaja_Permisos_YEliminar()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var editorPuquio = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        var licencia = await RegistrarAsync(editor, Datos("Kaspersky Endpoint", 10, Hoy.AddMonths(-1), Hoy.AddYears(1)));
        var (activo, _) = await CrearActivoAsync(editor, r);
        var (deBaja, _) = await CrearActivoAsync(editor, r, r.DeBaja);

        Assert.Equal("Licencias.EquipoDeBaja", await (await AsignarAsync(editor, licencia, deBaja)).LeerCodigoAsync());
        Assert.Equal("Licencias.SinEquipos", await (await AsignarAsync(editor, licencia)).LeerCodigoAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await editor.PostAsJsonAsync(
            "/api/licencias", Datos("Sin fechas", 1, Hoy, Hoy), Ct)).StatusCode);

        // Otra red no ve la licencia ni puede usar sus equipos; sin Licencias.Ver no se consulta.
        Assert.Equal(HttpStatusCode.NotFound, (await editorPuquio.GetAsync($"/api/licencias/{licencia}", Ct)).StatusCode);
        var dePuquio = await RegistrarAsync(editorPuquio, Datos("Office Puquio", 5, Hoy.AddMonths(-1), Hoy.AddYears(1)));
        Assert.Equal("Licencias.EquipoNoEncontrado", await (await AsignarAsync(editorPuquio, dePuquio, activo)).LeerCodigoAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await gestor.GetAsync("/api/licencias", Ct)).StatusCode);

        // El superadministrador indica la red; tampoco mezcla equipos de otra.
        Assert.Equal("Gestion.SedeObligatoria", await (await superAdmin.PostAsJsonAsync(
            "/api/licencias", Datos("Sin red", 1, Hoy, Hoy.AddYears(1)), Ct)).LeerCodigoAsync());
        var deSuper = await RegistrarAsync(superAdmin, Datos("Office Super", 5, Hoy, Hoy.AddYears(1), api.SedeHuantaId));
        Assert.Equal(HttpStatusCode.NoContent, (await AsignarAsync(superAdmin, deSuper, activo)).StatusCode);
        Assert.Equal("Licencias.EquipoNoEncontrado", await (await AsignarAsync(superAdmin, dePuquio, activo)).LeerCodigoAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await AsignarAsync(editor, licencia, activo)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await editor.DeleteAsync($"/api/licencias/{licencia}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await editor.GetAsync($"/api/licencias/{licencia}", Ct)).StatusCode);
        Assert.Equal(deSuper, Assert.Single((await editor.GetFromJsonAsync<List<LicenciaDelActivoDto>>($"/api/activos/{activo}/licencias", Json, Ct))!).LicenciaId);
    }
}
