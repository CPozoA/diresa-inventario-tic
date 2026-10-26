using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Activos.Consultar;
using Inventario.Application.Activos.Fotos;
using Inventario.Application.Catalogos;
using Inventario.Application.Common.Archivos;
using Inventario.Application.Common.Resultados;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Api.IntegrationTests;

/// <summary>
/// Fotos de los activos (D38, D47): subir con miniatura, la principal, el máximo de 6, solo imágenes, EXIF quitado,
/// permisos y aislamiento entre sedes, y que los archivos se borren con la foto o con el activo.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class FotosActivoTests(FabricaApi api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int Numero() => Random.Shared.Next(10_000_000, 99_999_999);

    /// <summary>Un monitor de Huanta (sin datos técnicos), registrado por el editor.</summary>
    private async Task<Guid> CrearActivoAsync()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var establecimiento = await superAdmin.PostAsJsonAsync(
            "/api/establecimientos",
            new { sedeId = api.SedeHuantaId, idEstablecimiento = Numero(), codigoUnico = Numero(), nombre = "Puesto de Salud de Fotos" },
            Ct);
        establecimiento.EnsureSuccessStatusCode();
        var area = await superAdmin.PostAsJsonAsync(
            "/api/catalogos/areas", new { sedeId = api.SedeHuantaId, nombre = $"Área {Guid.NewGuid():N}"[..20] }, Ct);
        area.EnsureSuccessStatusCode();

        async Task<Guid> Elemento(string catalogo, string nombre) =>
            (await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>($"/api/catalogos/{catalogo}", Json, Ct))!
                .Single(e => e.Nombre == nombre).Id;

        var ficha = new
        {
            establecimientoId = (await establecimiento.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id,
            generales = new
            {
                codigoPatrimonial = $"FOTO-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
                tipoActivoId = await Elemento("tipos-activo", "Monitor"),
                marcaId = await Elemento("marcas", "Dell"),
                modelo = "P2422H",
                areaId = (await area.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id,
                estadoActivoId = await Elemento("estados-activo", "Operativo"),
                conservacion = "Bueno",
            },
            discos = Array.Empty<object>(),
        };

        var respuesta = await editor.PostAsJsonAsync("/api/activos", ficha, Ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
    }

    /// <summary>JPEG mínima con EXIF (ubicación GPS y modelo del celular).</summary>
    private static byte[] Jpeg(string marca = "imagen") =>
    [
        0xFF, 0xD8,
        .. Segmento(0xE0, "JFIF\0\u0001\u0001\0\0\u0001\0\u0001\0\0"u8),
        .. Segmento(0xE1, "Exif\0\0GPS -12.93,-74.24 Samsung"u8),
        .. Segmento(0xDB, [0x00, 1, 2, 3, 4]),
        .. Segmento(0xDA, [0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]),
        .. Encoding.ASCII.GetBytes(marca),
        0xFF, 0xD9,
    ];

    private static byte[] Segmento(byte marcador, ReadOnlySpan<byte> datos)
    {
        var longitud = datos.Length + 2;
        return [0xFF, marcador, (byte)(longitud >> 8), (byte)longitud, .. datos];
    }

    private static bool Contiene(byte[] contenido, string texto) =>
        contenido.AsSpan().IndexOf(Encoding.ASCII.GetBytes(texto)) >= 0;

    private static Task<HttpResponseMessage> SubirAsync(
        HttpClient cliente, Guid activoId, byte[]? foto, byte[]? miniatura = null, string? descripcion = null)
    {
        var formulario = new MultipartFormDataContent();
        if (foto is not null)
        {
            formulario.Add(Archivo(foto), "archivo", "foto.jpg");
        }

        formulario.Add(Archivo(miniatura ?? Jpeg("mini")), "miniatura", "miniatura.jpg");
        if (descripcion is not null)
        {
            formulario.Add(new StringContent(descripcion), "descripcion");
        }

        return cliente.PostAsync($"/api/activos/{activoId}/fotos", formulario, Ct);

        static ByteArrayContent Archivo(byte[] contenido)
        {
            var parte = new ByteArrayContent(contenido);
            parte.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return parte;
        }
    }

    private static async Task<FotoActivoDto> SubirBienAsync(HttpClient cliente, Guid activoId, string? descripcion = null)
    {
        var respuesta = await SubirAsync(cliente, activoId, Jpeg(), descripcion: descripcion);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<FotoActivoDto>(Json, Ct))!;
    }

    private static async Task<List<FotoActivoDto>> ListarAsync(HttpClient cliente, Guid activoId) =>
        (await cliente.GetFromJsonAsync<List<FotoActivoDto>>($"/api/activos/{activoId}/fotos", Json, Ct))!;

    private static async Task<string?> CodigoErrorAsync(HttpResponseMessage respuesta)
    {
        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct));
        return documento.RootElement.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null;
    }

    /// <summary>¿Sigue el archivo en el almacén?</summary>
    private async Task<bool> ExisteArchivoAsync(Guid activoId, Guid fotoId, bool miniatura = false)
    {
        var almacen = api.Services.GetRequiredService<IAlmacenArchivos>();
        await using var archivo = await almacen.AbrirAsync(
            $"{api.SedeHuantaId:N}/{activoId:N}/{fotoId:N}{(miniatura ? "-mini" : "")}.jpg", Ct);
        return archivo is not null;
    }

    [Fact]
    public async Task Editor_SubeUnaFoto_SinExif_YQuedaComoPrincipalEnLaLista()
    {
        var activoId = await CrearActivoAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var respuesta = await SubirAsync(editor, activoId, Jpeg("foto-grande"), Jpeg("foto-chica"), "  Placa patrimonial ");
        var foto = (await respuesta.Content.ReadFromJsonAsync<FotoActivoDto>(Json, Ct))!;
        var archivo = await editor.GetAsync($"/api/activos/{activoId}/fotos/{foto.Id}/archivo", Ct);
        var bytes = await archivo.Content.ReadAsByteArrayAsync(Ct);
        var miniatura = await editor.GetByteArrayAsync($"/api/activos/{activoId}/fotos/{foto.Id}/archivo?miniatura=true", Ct);
        var pagina = await editor.GetFromJsonAsync<PaginaActivosDto>("/api/activos?tamanoPagina=100", Json, Ct);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal($"/api/activos/{activoId}/fotos/{foto.Id}", respuesta.Headers.Location?.OriginalString);
        Assert.True(foto.Principal);
        Assert.Equal("Placa patrimonial", foto.Descripcion);
        Assert.Equal("image/jpeg", archivo.Content.Headers.ContentType?.MediaType);
        Assert.True(archivo.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromDays(365), archivo.Headers.CacheControl?.MaxAge);
        Assert.Equal("nosniff", archivo.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(Contiene(bytes, "foto-grande"));
        Assert.False(Contiene(bytes, "GPS"));
        Assert.False(Contiene(bytes, "Samsung"));
        Assert.Equal(foto.TamanoBytes, bytes.Length);
        Assert.True(Contiene(miniatura, "foto-chica"));
        Assert.False(Contiene(miniatura, "GPS"));
        Assert.Equal(foto.Id, pagina!.Activos.Single(a => a.Id == activoId).FotoPrincipalId);
    }

    [Fact]
    public async Task LaPrincipalSePuedeCambiar_YSiSeEliminaPasaASerloLaSiguiente()
    {
        var activoId = await CrearActivoAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var primera = await SubirBienAsync(editor, activoId, "Vista frontal");
        var segunda = await SubirBienAsync(editor, activoId, "Etiqueta de serie");
        var tercera = await SubirBienAsync(editor, activoId);

        var marcar = await editor.PostAsync($"/api/activos/{activoId}/fotos/{tercera.Id}/principal", null, Ct);
        var trasMarcar = await ListarAsync(editor, activoId);
        var eliminar = await editor.DeleteAsync($"/api/activos/{activoId}/fotos/{tercera.Id}", Ct);
        var trasEliminar = await ListarAsync(editor, activoId);

        Assert.False(segunda.Principal);
        Assert.Equal(HttpStatusCode.NoContent, marcar.StatusCode);
        Assert.Equal([tercera.Id, primera.Id, segunda.Id], trasMarcar.Select(f => f.Id));
        Assert.Single(trasMarcar, f => f.Principal);
        Assert.Equal(HttpStatusCode.NoContent, eliminar.StatusCode);
        Assert.Equal([primera.Id, segunda.Id], trasEliminar.Select(f => f.Id));
        Assert.True(trasEliminar[0].Principal);
        Assert.False(await ExisteArchivoAsync(activoId, tercera.Id));
        Assert.False(await ExisteArchivoAsync(activoId, tercera.Id, miniatura: true));
        Assert.True(await ExisteArchivoAsync(activoId, primera.Id));
    }

    [Fact]
    public async Task UnActivoTieneComoMaximoSeisFotos()
    {
        var activoId = await CrearActivoAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        for (var i = 0; i < 6; i++)
        {
            await SubirBienAsync(editor, activoId);
        }

        var septima = await SubirAsync(editor, activoId, Jpeg());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, septima.StatusCode);
        Assert.Equal("Fotos.Maximo", await CodigoErrorAsync(septima));
        Assert.Equal(6, (await ListarAsync(editor, activoId)).Count);
    }

    [Fact]
    public async Task SoloSeAceptanImagenes_YLaDescripcionNoPasaDe60Caracteres()
    {
        var activoId = await CrearActivoAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var foto = await SubirBienAsync(editor, activoId);

        var pdf = await SubirAsync(editor, activoId, [.. "%PDF-1.7 documento"u8]);
        var sinArchivo = await SubirAsync(editor, activoId, null);
        var cortada = await SubirAsync(editor, activoId, Jpeg().AsSpan(0, 20).ToArray());
        var larga = await editor.PutAsJsonAsync($"/api/activos/{activoId}/fotos/{foto.Id}", new { descripcion = new string('x', 61) }, Ct);
        var bien = await editor.PutAsJsonAsync($"/api/activos/{activoId}/fotos/{foto.Id}", new { descripcion = "Puertos laterales" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, pdf.StatusCode);
        Assert.Equal("Fotos.TipoNoValido", await CodigoErrorAsync(pdf));
        Assert.Equal("Fotos.SinArchivo", await CodigoErrorAsync(sinArchivo));
        Assert.Equal("Fotos.Danada", await CodigoErrorAsync(cortada));
        Assert.Equal(HttpStatusCode.BadRequest, larga.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, bien.StatusCode);
        Assert.Equal("Puertos laterales", Assert.Single(await ListarAsync(editor, activoId)).Descripcion);
    }

    [Fact]
    public async Task QuienSoloVeActivos_VeLasFotosPeroNoLasCambia_YOtraSedeNoLasVe()
    {
        var activoId = await CrearActivoAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);
        using var puquio = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);
        var foto = await SubirBienAsync(editor, activoId);
        var archivo = $"/api/activos/{activoId}/fotos/{foto.Id}/archivo";

        var gestorLista = await gestor.GetAsync($"/api/activos/{activoId}/fotos", Ct);
        var gestorArchivo = await gestor.GetAsync(archivo, Ct);
        var gestorSube = await SubirAsync(gestor, activoId, Jpeg());
        var gestorElimina = await gestor.DeleteAsync($"/api/activos/{activoId}/fotos/{foto.Id}", Ct);
        var puquioLista = await puquio.GetAsync($"/api/activos/{activoId}/fotos", Ct);
        var puquioArchivo = await puquio.GetAsync(archivo, Ct);
        var puquioSube = await SubirAsync(puquio, activoId, Jpeg());
        var puquioElimina = await puquio.DeleteAsync($"/api/activos/{activoId}/fotos/{foto.Id}", Ct);

        Assert.Equal(HttpStatusCode.OK, gestorLista.StatusCode);
        Assert.Equal(HttpStatusCode.OK, gestorArchivo.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, gestorSube.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, gestorElimina.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, puquioLista.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, puquioArchivo.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, puquioSube.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, puquioElimina.StatusCode);
        Assert.True(await ExisteArchivoAsync(activoId, foto.Id));
    }

    [Fact]
    public async Task AlEliminarElActivo_SeBorranSusFotosYArchivos()
    {
        var activoId = await CrearActivoAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var foto = await SubirBienAsync(editor, activoId);

        var eliminar = await admin.DeleteAsync($"/api/activos/{activoId}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, eliminar.StatusCode);
        Assert.False(await ExisteArchivoAsync(activoId, foto.Id));
        Assert.False(await ExisteArchivoAsync(activoId, foto.Id, miniatura: true));
    }
}
