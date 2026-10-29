using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Activos.Consultar;
using Inventario.Application.Catalogos;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Mantenimientos;
using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;

namespace Inventario.Api.IntegrationTests;

/// <summary>Mantenimiento de los activos (D53, §5.13).</summary>
[Collection(ColeccionApi.Nombre)]
public sealed class MantenimientosTests(FabricaApi api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    private static int Numero() => Random.Shared.Next(10_000_000, 99_999_999);

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private sealed record Referencias(Guid Establecimiento, Guid Area, Guid Monitor, Guid Marca, Guid Operativo, Guid Inoperativo, Guid DeBaja);

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
            await Crear("/api/establecimientos", new { sedeId = api.SedeHuantaId, idEstablecimiento = Numero(), codigoUnico = Numero(), nombre = "C.S. Mantenimiento" }),
            await Crear("/api/catalogos/areas", new { sedeId = api.SedeHuantaId, nombre = $"Área {Guid.NewGuid():N}"[..20] }),
            await Elemento("tipos-activo", "Monitor"),
            await Elemento("marcas", "Dell"),
            await Elemento("estados-activo", "Operativo"),
            await Elemento("estados-activo", "Inoperativo"),
            await Elemento("estados-activo", "De baja"));
    }

    private static async Task<(Guid Id, string Codigo)> CrearActivoAsync(HttpClient cliente, Referencias r, Guid? estado = null)
    {
        var codigo = $"MNT-{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/activos",
            new
            {
                establecimientoId = r.Establecimiento,
                generales = new
                {
                    codigoPatrimonial = codigo,
                    tipoActivoId = r.Monitor,
                    marcaId = r.Marca,
                    modelo = "P2422H",
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

    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, Guid activo, object cuerpo) =>
        cliente.PostAsJsonAsync($"/api/activos/{activo}/mantenimientos", cuerpo, Ct);

    private static object Correctivo(DateOnly inicio, string? detalle = "No enciende") => new
    {
        tipo = "Correctivo",
        fechaInicio = Fecha(inicio),
        detalle,
        ejecutor = "Proveedor",
        proveedor = "Servicios Técnicos Wari S.A.C.",
        ordenServicio = "os-2026-118",
        costo = 350m,
    };

    private static object Cierre(DateOnly fin, Guid estado, DateOnly? proximo, string conservacion = "Bueno", decimal? costo = null) => new
    {
        fechaFin = Fecha(fin),
        trabajoRealizado = "Cambio de fuente de poder",
        resultado = "Operativo",
        costo,
        estadoActivoId = estado,
        conservacion,
        proximoMantenimiento = proximo is DateOnly p ? Fecha(p) : null,
    };

    private static Task<MantenimientosActivoDto?> DelActivoAsync(HttpClient cliente, Guid activo) =>
        cliente.GetFromJsonAsync<MantenimientosActivoDto>($"/api/activos/{activo}/mantenimientos", Json, Ct);

    private static Task<FichaActivoDto?> FichaAsync(HttpClient cliente, Guid activo) =>
        cliente.GetFromJsonAsync<FichaActivoDto>($"/api/activos/{activo}", Json, Ct);

    [Fact]
    public async Task Correctivo_PoneElActivoEnMantenimiento_YAlCerrarQuedaComoSeIndica()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var (activo, codigo) = await CrearActivoAsync(editor, r);

        Assert.Equal(HttpStatusCode.Created, (await RegistrarAsync(editor, activo, Correctivo(Hoy.AddDays(-5)))).StatusCode);

        var ficha = await FichaAsync(editor, activo);
        Assert.Equal(GrupoEstado.EnMantenimiento, ficha!.GrupoEstado);
        var enCurso = (await DelActivoAsync(editor, activo))!.EnCurso!;
        Assert.Equal("OS-2026-118", enCurso.OrdenServicio);
        Assert.Equal(350m, enCurso.Costo);

        var otro = await RegistrarAsync(editor, activo, Correctivo(Hoy));
        Assert.Equal(HttpStatusCode.Conflict, otro.StatusCode);
        Assert.Equal("Mantenimientos.YaHayUnoAbierto", await otro.LeerCodigoAsync());

        var lista = await editor.GetFromJsonAsync<PaginaMantenimientosDto>($"/api/mantenimientos?vista=EnCurso&buscar={codigo}", Json, Ct);
        Assert.Equal(enCurso.Id, Assert.Single(lista!.Mantenimientos).Id);

        var antesDelInicio = await editor.PostAsJsonAsync($"/api/mantenimientos/{enCurso.Id}/cerrar", Cierre(Hoy.AddDays(-6), r.Operativo, null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, antesDelInicio.StatusCode);

        var cerrar = await editor.PostAsJsonAsync(
            $"/api/mantenimientos/{enCurso.Id}/cerrar", Cierre(Hoy, r.Operativo, Hoy.AddMonths(6), "Regular", 380m), Ct);
        Assert.Equal(HttpStatusCode.NoContent, cerrar.StatusCode);

        var despues = await FichaAsync(editor, activo);
        Assert.Equal(r.Operativo, despues!.Generales.Estado.Id);
        Assert.Equal(Conservacion.Regular, despues.Generales.Conservacion);
        var mantenimiento = (await DelActivoAsync(editor, activo))!;
        Assert.Null(mantenimiento.EnCurso);
        Assert.Equal(Hoy.AddMonths(6), mantenimiento.ProximoMantenimiento);
        Assert.Equal(380m, Assert.Single(mantenimiento.Historial).Costo);

        var otraVez = await editor.PostAsJsonAsync($"/api/mantenimientos/{enCurso.Id}/cerrar", Cierre(Hoy, r.Operativo, null), Ct);
        Assert.Equal("Mantenimientos.YaCerrado", await otraVez.LeerCodigoAsync());

        var historial = await editor.GetFromJsonAsync<PaginaMantenimientosDto>($"/api/mantenimientos?vista=Historial&buscar={codigo}", Json, Ct);
        Assert.Single(historial!.Mantenimientos);
    }

    [Fact]
    public async Task PreventivoTerminado_ProgramaElSiguiente_YVencidoApareceEnProgramadosYEnAtencion()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var tecnico = await api.ObtenerUsuarioIdAsync(DatosPrueba.EditorHuanta);
        var administrador = await api.ObtenerUsuarioIdAsync(DatosPrueba.AdminHuanta);
        var sinPermiso = await api.ObtenerUsuarioIdAsync(DatosPrueba.GestorHuanta);
        var (activo, codigo) = await CrearActivoAsync(editor, r);

        // Técnicos: quienes gestionan mantenimientos en la red (también el administrador de sede), no los demás.
        var tecnicos = (await editor.GetFromJsonAsync<List<TecnicoDto>>("/api/mantenimientos/tecnicos", Json, Ct))!;
        Assert.Contains(tecnicos, t => t.Id == tecnico);
        Assert.Contains(tecnicos, t => t.Id == administrador);
        Assert.DoesNotContain(tecnicos, t => t.Id == sinPermiso);

        // Hecho hace 200 días, con el siguiente hace 10 días: ya está vencido.
        var registro = await RegistrarAsync(editor, activo, new
        {
            tipo = "Preventivo",
            fechaInicio = Fecha(Hoy.AddDays(-200)),
            ejecutor = "PersonalTi",
            tecnicoId = tecnico,
            cierre = Cierre(Hoy.AddDays(-200), r.Operativo, Hoy.AddDays(-10)),
        });
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        Assert.Equal(GrupoEstado.EnUso, (await FichaAsync(editor, activo))!.GrupoEstado);

        var programados = await editor.GetFromJsonAsync<List<ProgramadoDto>>($"/api/mantenimientos/programados?buscar={codigo}", Json, Ct);
        var fila = Assert.Single(programados!);
        Assert.True(fila.Vencido);
        Assert.Equal(Hoy.AddDays(-200), fila.UltimoPreventivo);

        var atencion = await editor.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?atencion=MantenimientoVencido&buscar={codigo}", Json, Ct);
        Assert.Single(atencion!.Activos);
        Assert.True((await editor.GetFromJsonAsync<CifrasMantenimientoDto>("/api/mantenimientos/cifras", Json, Ct))!.Vencidos >= 1);

        // Con un mantenimiento en curso deja de figurar como vencido.
        Assert.Equal(HttpStatusCode.Created, (await RegistrarAsync(editor, activo, Correctivo(Hoy))).StatusCode);
        Assert.Empty((await editor.GetFromJsonAsync<List<ProgramadoDto>>($"/api/mantenimientos/programados?buscar={codigo}", Json, Ct))!);
        Assert.Empty((await editor.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?atencion=MantenimientoVencido&buscar={codigo}", Json, Ct))!.Activos);

        // Al cerrar el correctivo puede quedar el preventivo vencido que ya tenía: vuelve a figurar en Programados.
        var correctivo = (await DelActivoAsync(editor, activo))!.EnCurso!;
        var cierre = await editor.PostAsJsonAsync(
            $"/api/mantenimientos/{correctivo.Id}/cerrar", Cierre(Hoy, r.Operativo, Hoy.AddDays(-10), costo: 350m), Ct);
        Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);
        Assert.True(Assert.Single((await editor.GetFromJsonAsync<List<ProgramadoDto>>($"/api/mantenimientos/programados?buscar={codigo}", Json, Ct))!).Vencido);
    }

    [Fact]
    public async Task Reglas_TecnicoDeLaRed_ProblemaEnElCorrectivo_FechasYActivosDeBaja()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var editorPuquio = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);
        var (activo, _) = await CrearActivoAsync(editor, r);

        object Interno(Guid tecnico) => new { tipo = "Preventivo", fechaInicio = Fecha(Hoy), ejecutor = "PersonalTi", tecnicoId = tecnico };

        var deOtraRed = await RegistrarAsync(editor, activo, Interno(await api.ObtenerUsuarioIdAsync(DatosPrueba.EditorPuquio)));
        Assert.Equal("Mantenimientos.TecnicoInvalido", await deOtraRed.LeerCodigoAsync());
        var sinPermiso = await RegistrarAsync(editor, activo, Interno(await api.ObtenerUsuarioIdAsync(DatosPrueba.GestorHuanta)));
        Assert.Equal("Mantenimientos.TecnicoInvalido", await sinPermiso.LeerCodigoAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await RegistrarAsync(editor, activo, Correctivo(Hoy, detalle: null))).StatusCode);
        Assert.Equal("Mantenimientos.FechaFutura", await (await RegistrarAsync(editor, activo, Correctivo(Hoy.AddDays(1)))).LeerCodigoAsync());

        var (deBaja, _) = await CrearActivoAsync(editor, r, r.DeBaja);
        Assert.Equal("Mantenimientos.ActivoDeBaja", await (await RegistrarAsync(editor, deBaja, Correctivo(Hoy))).LeerCodigoAsync());

        // Otra red no ve el activo; sin Mantenimiento.Ver no se consulta.
        Assert.Equal(HttpStatusCode.NotFound, (await editorPuquio.GetAsync($"/api/activos/{activo}/mantenimientos", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await gestor.GetAsync("/api/mantenimientos", Ct)).StatusCode);
    }
}
