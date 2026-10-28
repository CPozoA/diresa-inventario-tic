using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Activos.Consultar;
using Inventario.Application.Catalogos;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Establecimientos.Listar;
using Inventario.Application.Resumen;
using Inventario.Domain.Catalogos;

namespace Inventario.Api.IntegrationTests;

/// <summary>
/// Resumen del inicio (§5.11, D48, D49). Se arma en una red nueva, con cinco activos conocidos, para que las cifras
/// sean exactas aunque otras pruebas registren activos en Huanta o Puquio.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class ResumenTests(FabricaApi api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int Numero() => Random.Shared.Next(10_000_000, 99_999_999);

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>La red de prueba y sus activos.</summary>
    private sealed record Escenario(
        Guid SedeId,
        Guid Establecimiento1,
        Guid Establecimiento2,
        Guid Establecimiento3,
        Guid A1,
        Guid A2,
        Guid A3,
        Guid A4,
        Guid A5);

    /// <summary>
    /// Red nueva con tres establecimientos (el tercero sin activos) y cinco activos:
    /// A1 operativo, comprado hace 1 año, ficha completa con foto;
    /// A2 operativo, 6 años de uso y 5 de vida útil (renovar), garantía vencida;
    /// A3 inoperativo, le quedan 6 meses de vida útil, garantía vencida;
    /// A4 de baja, 10 años de uso y garantía vencida (no cuenta en las alertas);
    /// A5 en mantenimiento, sin fecha, sin serie ni responsable.
    /// </summary>
    private static async Task<Escenario> PrepararAsync(HttpClient superAdmin)
    {
        var sede = await superAdmin.PostAsJsonAsync("/api/sedes", new { codigo = $"R-{Guid.NewGuid():N}"[..14], nombre = "Red de resumen" }, Ct);
        sede.EnsureSuccessStatusCode();
        var sedeId = (await sede.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;

        async Task<Guid> Crear(string url, object cuerpo)
        {
            var respuesta = await superAdmin.PostAsJsonAsync(url, cuerpo, Ct);
            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
        }

        Task<Guid> Establecimiento(string nombre) =>
            Crear("/api/establecimientos", new { sedeId, idEstablecimiento = Numero(), codigoUnico = Numero(), nombre });

        async Task<Guid> Elemento(string catalogo, string nombre) =>
            (await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>($"/api/catalogos/{catalogo}", Json, Ct))!
                .Single(e => e.Nombre == nombre).Id;

        var e1 = await Establecimiento("C.S. Resumen");
        var e2 = await Establecimiento("P.S. Resumen");
        var e3 = await Establecimiento("P.S. Sin equipos");
        var area = await Crear("/api/catalogos/areas", new { sedeId, nombre = "Administración" });
        var monitor = await Elemento("tipos-activo", "Monitor");
        var ups = await Elemento("tipos-activo", "UPS");
        var marca = await Elemento("marcas", "Dell");
        var operativo = await Elemento("estados-activo", "Operativo");
        var inoperativo = await Elemento("estados-activo", "Inoperativo");
        var deBaja = await Elemento("estados-activo", "De baja");
        var mantenimiento = await Elemento("estados-activo", "En mantenimiento");

        Task<Guid> Activo(
            Guid establecimiento, Guid tipo, Guid estado, DateOnly? adquirido, DateOnly? finGarantia, string? serie, string? responsable) =>
            Crear("/api/activos", new
            {
                establecimientoId = establecimiento,
                generales = new
                {
                    codigoPatrimonial = $"RES-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
                    tipoActivoId = tipo,
                    marcaId = marca,
                    modelo = "Modelo de resumen",
                    numeroSerie = serie,
                    areaId = area,
                    responsable,
                    estadoActivoId = estado,
                    conservacion = "Bueno",
                },
                adquisicion = new
                {
                    fechaAdquisicion = adquirido is DateOnly a ? Fecha(a) : null,
                    vidaUtilAnios = adquirido is null ? (int?)null : 5,
                    finGarantia = finGarantia is DateOnly g ? Fecha(g) : null,
                },
                discos = Array.Empty<object>(),
            });

        var a1 = await Activo(e1, monitor, operativo, Hoy.AddYears(-1), Hoy.AddYears(1), "SN-A1", "Juan Pérez");
        var a2 = await Activo(e1, monitor, operativo, Hoy.AddYears(-6), Hoy.AddYears(-2), "SN-A2", "Rosa Huamán");
        var a3 = await Activo(e2, ups, inoperativo, Hoy.AddYears(-4).AddMonths(-6), Hoy.AddYears(-1), "SN-A3", "Luis Ccorahua");
        var a4 = await Activo(e2, monitor, deBaja, Hoy.AddYears(-10), Hoy.AddYears(-8), null, null);
        var a5 = await Activo(e1, ups, mantenimiento, null, null, null, null);

        // A1 queda con la ficha completa: serie, fecha, responsable y una foto.
        using var formulario = new MultipartFormDataContent();
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00, 0x12, 0x34, 0xFF, 0xD9];
        foreach (var nombre in new[] { "archivo", "miniatura" })
        {
            var parte = new ByteArrayContent(jpeg);
            parte.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            formulario.Add(parte, nombre, "foto.jpg");
        }

        var foto = await superAdmin.PostAsync($"/api/activos/{a1}/fotos", formulario, Ct);
        Assert.Equal(HttpStatusCode.Created, foto.StatusCode);

        return new Escenario(sedeId, e1, e2, e3, a1, a2, a3, a4, a5);
    }

    [Fact]
    public async Task ElResumenDeUnaRed_CuentaLoQueTiene_YLosDeBajaNoEntranEnLasAlertas()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var red = await PrepararAsync(superAdmin);

        var resumen = (await superAdmin.GetFromJsonAsync<ResumenDto>($"/api/resumen?sedeId={red.SedeId}", Json, Ct))!;

        Assert.Equal(red.SedeId, resumen.SedeId);
        Assert.Equal("Red de resumen", resumen.SedeNombre);
        Assert.Null(resumen.Redes);
        Assert.Equal(new CifrasResumenDto(Activos: 5, EnUso: 2, Renovar: 1, ProximosARenovar: 1, GarantiaVencida: 2), resumen.Cifras);

        Assert.Equal(
            [(GrupoEstado.EnUso, 2), (GrupoEstado.EnMantenimiento, 1), (GrupoEstado.FueraDeServicio, 1), (GrupoEstado.DeBaja, 1)],
            resumen.Estados.Select(e => (e.Grupo, e.Cantidad)));
        Assert.Equal(
            [(RangoAntiguedad.MenosDe2, 1), (RangoAntiguedad.De2A4, 0), (RangoAntiguedad.De4A6, 1), (RangoAntiguedad.MasDe6, 1), (RangoAntiguedad.SinFecha, 1)],
            resumen.Antiguedad.Select(a => (a.Rango, a.Cantidad)));
        Assert.Equal([("Monitor", 3), ("UPS", 2)], resumen.Tipos.Select(t => (t.Nombre, t.Cantidad)));

        Assert.Equal(3, resumen.Establecimientos.Total);
        Assert.Equal(1, resumen.Establecimientos.SinActivos);
        Assert.Equal(
            [new CifrasEstablecimientoDto(red.Establecimiento1, "C.S. Resumen", 3, 2, 1), new CifrasEstablecimientoDto(red.Establecimiento2, "P.S. Resumen", 2, 0, 0)],
            resumen.Establecimientos.Principales.AsEnumerable());

        Assert.Equal(3, resumen.Atencion.Total);
        Assert.Equal(
            [(red.A3, MotivoAtencion.FueraDeServicio), (red.A2, MotivoAtencion.Renovar), (red.A5, MotivoAtencion.EnMantenimiento)],
            resumen.Atencion.Primeros.Select(a => (a.Id, a.Motivo)));

        Assert.Equal(new FichasResumenDto(Evaluadas: 4, Completas: 1, SinFotos: 3, SinFechaAdquisicion: 1, SinResponsable: 1, SinNumeroSerie: 1), resumen.Fichas);
    }

    [Fact]
    public async Task LosFiltrosDelInventario_DanLasMismasCifrasQueElResumen()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var red = await PrepararAsync(superAdmin);

        async Task<List<Guid>> Ids(string filtros) =>
            (await superAdmin.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?sedeId={red.SedeId}&{filtros}", Json, Ct))!
                .Activos.Select(a => a.Id).Order().ToList();

        static List<Guid> Ordenados(params Guid[] ids) => ids.Order().ToList();

        Assert.Equal(Ordenados(red.A2, red.A3, red.A5), await Ids("atencion=Cualquiera"));
        Assert.Equal(Ordenados(red.A2), await Ids("atencion=Renovar"));
        Assert.Equal(Ordenados(red.A2, red.A3), await Ids("atencion=GarantiaVencida"));
        Assert.Equal(Ordenados(red.A2, red.A3, red.A5), await Ids("falta=Fotos"));
        Assert.Equal(Ordenados(red.A5), await Ids("falta=FechaAdquisicion"));
        // Sin filtro de atención, el de baja sí aparece en la lista.
        Assert.Contains(red.A4, await Ids("garantia=Vencida"));

        var sinActivos = await superAdmin.GetFromJsonAsync<List<EstablecimientoDto>>(
            $"/api/establecimientos?sedeId={red.SedeId}&sinActivos=true", Json, Ct);
        Assert.Equal(red.Establecimiento3, Assert.Single(sinActivos!).Id);
    }

    [Fact]
    public async Task ElSuperAdmin_VeLaTablaPorRed_YUnUsuarioDeSedeSoloLaSuya()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var red = await PrepararAsync(superAdmin);

        var todas = (await superAdmin.GetFromJsonAsync<ResumenDto>("/api/resumen", Json, Ct))!;
        var deHuanta = (await editor.GetFromJsonAsync<ResumenDto>($"/api/resumen?sedeId={red.SedeId}", Json, Ct))!;

        Assert.Null(todas.SedeId);
        var fila = Assert.Single(todas.Redes!, r => r.SedeId == red.SedeId);
        Assert.Equal(new CifrasRedDto(red.SedeId, "Red de resumen", 5, 2, 1, 1, 2, 4, 1, 3, 1), fila);
        Assert.Contains(todas.Redes!, r => r.SedeId == api.SedeHuantaId);

        // El parámetro no le sirve a un usuario de sede: siempre ve la suya.
        Assert.Equal(api.SedeHuantaId, deHuanta.SedeId);
        Assert.Null(deHuanta.Redes);
        Assert.DoesNotContain(deHuanta.Atencion.Primeros, a => a.Id == red.A2 || a.Id == red.A3);
    }

    [Fact]
    public async Task SoloElSuperAdminCambiaElGrupoDeUnEstado()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var crear = await superAdmin.PostAsJsonAsync("/api/catalogos/estados-activo", new { nombre = $"Prestado {Guid.NewGuid():N}"[..20] }, Ct);
        var id = (await crear.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;

        var delAdmin = await admin.PutAsJsonAsync($"/api/catalogos/estados-activo/{id}/grupo", new { grupo = "EnAlmacen" }, Ct);
        var delSuper = await superAdmin.PutAsJsonAsync($"/api/catalogos/estados-activo/{id}/grupo", new { grupo = "EnAlmacen" }, Ct);
        var estados = await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>("/api/catalogos/estados-activo?incluirInactivos=true", Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, delAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delSuper.StatusCode);
        Assert.Equal(GrupoEstado.EnAlmacen, estados!.Single(e => e.Id == id).Grupo);
        Assert.Equal(GrupoEstado.DeBaja, estados!.Single(e => e.Nombre == "De baja").Grupo);
        Assert.Null((await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>("/api/catalogos/marcas", Json, Ct))![0].Grupo);
    }
}
