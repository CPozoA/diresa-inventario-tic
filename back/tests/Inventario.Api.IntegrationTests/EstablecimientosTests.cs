using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Establecimientos.Importar;
using Inventario.Application.Establecimientos.Listar;

namespace Inventario.Api.IntegrationTests;

/// <summary>
/// Establecimientos (docs/ARQUITECTURA.md §5.9): solo el superadministrador los registra e importa; cada usuario de sede
/// solo ve los de su red. La importación desde Excel revisa todas las filas y, si alguna falla, no guarda nada.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class EstablecimientosTests(FabricaApi api)
{
    private const string TipoExcel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Número al azar para Id_Establecimiento y código único: la base de pruebas se comparte entre pruebas.</summary>
    private static int Numero() => Random.Shared.Next(10_000_000, 99_999_999);

    private static object Nuevo(Guid sedeId, int id, int codigo, string nombre = "Puesto de Salud de Pruebas", string? ubigeo = "050601") =>
        new { sedeId, idEstablecimiento = id, codigoUnico = codigo, nombre, categoria = "I-1", microred = "Microred Pruebas", provincia = "Huanta", distrito = "Huanta", ubigeo };

    private static async Task<List<EstablecimientoDto>> BuscarAsync(HttpClient cliente, int codigo, bool incluirInactivos = false) =>
        await cliente.GetFromJsonAsync<List<EstablecimientoDto>>(
            $"/api/establecimientos?buscar={codigo}&incluirInactivos={incluirInactivos}",
            Ct) ?? [];

    private async Task<(Guid Id, int IdEstablecimiento, int Codigo)> CrearEnHuantaAsync(HttpClient superAdmin)
    {
        var id = Numero();
        var codigo = Numero();
        var respuesta = await superAdmin.PostAsJsonAsync("/api/establecimientos", Nuevo(api.SedeHuantaId, id, codigo), Ct);
        respuesta.EnsureSuccessStatusCode();
        var creado = await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct);
        return (creado!.Id, id, codigo);
    }

    [Fact]
    public async Task SuperAdmin_Crea_YSoloLoVeLaRedALaQuePertenece()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        using var huanta = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var puquio = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);

        var (_, _, codigo) = await CrearEnHuantaAsync(superAdmin);

        var vistoEnHuanta = await BuscarAsync(huanta, codigo);
        var vistoEnPuquio = await BuscarAsync(puquio, codigo);

        var encontrado = Assert.Single(vistoEnHuanta);
        Assert.Equal(api.SedeHuantaId, encontrado.SedeId);
        Assert.Equal("Red de Salud Huanta", encontrado.SedeNombre);
        Assert.Empty(vistoEnPuquio);
    }

    [Fact]
    public async Task AdministradorDeSede_NoPuedeCrearNiImportar()
    {
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);

        var crear = await admin.PostAsJsonAsync("/api/establecimientos", Nuevo(api.SedeHuantaId, Numero(), Numero()), Ct);
        using var contenido = Archivo(Excel(["Id_Establecimiento", "Codigo_Unico", "Nombre", "Red"]));
        var importar = await admin.PostAsync("/api/establecimientos/importar", contenido, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, importar.StatusCode);
    }

    [Fact]
    public async Task CodigoUnicoRepetido_Responde409()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var (_, _, codigo) = await CrearEnHuantaAsync(superAdmin);

        var respuesta = await superAdmin.PostAsJsonAsync("/api/establecimientos", Nuevo(api.SedePuquioId, Numero(), codigo), Ct);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("Establecimientos.CodigoUnicoDuplicado", await respuesta.LeerCodigoAsync());
    }

    [Fact]
    public async Task UbigeoQueNoTieneSeisDigitos_Responde400()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        var respuesta = await superAdmin.PostAsJsonAsync(
            "/api/establecimientos",
            Nuevo(api.SedeHuantaId, Numero(), Numero(), ubigeo: "5060"),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Editar_CorrigeLosDatosSinCambiarDeRed()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var (id, idEstablecimiento, codigo) = await CrearEnHuantaAsync(superAdmin);

        var editar = await superAdmin.PutAsJsonAsync(
            $"/api/establecimientos/{id}",
            new { idEstablecimiento, codigoUnico = codigo, nombre = "Centro de Salud Corregido", categoria = "I-3", ubigeo = "050602" },
            Ct);
        var guardado = Assert.Single(await BuscarAsync(superAdmin, codigo));

        Assert.Equal(HttpStatusCode.NoContent, editar.StatusCode);
        Assert.Equal("Centro de Salud Corregido", guardado.Nombre);
        Assert.Equal("I-3", guardado.Categoria);
        Assert.Null(guardado.Microred);
        Assert.Equal(api.SedeHuantaId, guardado.SedeId);
    }

    [Fact]
    public async Task Desactivado_NoSeOfrece_PeroSeConserva()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var (id, _, codigo) = await CrearEnHuantaAsync(superAdmin);

        var desactivar = await superAdmin.PostAsync($"/api/establecimientos/{id}/desactivar", content: null, Ct);
        var activos = await BuscarAsync(superAdmin, codigo);
        var todos = await BuscarAsync(superAdmin, codigo, incluirInactivos: true);

        Assert.Equal(HttpStatusCode.NoContent, desactivar.StatusCode);
        Assert.Empty(activos);
        Assert.False(Assert.Single(todos).Activo);
    }

    [Fact]
    public async Task Plantilla_EsUnExcelConLosEncabezados()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        var respuesta = await superAdmin.GetAsync("/api/establecimientos/plantilla", Ct);
        var bytes = await respuesta.Content.ReadAsByteArrayAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(TipoExcel, respuesta.Content.Headers.ContentType?.MediaType);
        using var libro = new XLWorkbook(new MemoryStream(bytes));
        var hoja = libro.Worksheets.First();
        Assert.Equal("Id_Establecimiento", hoja.Cell(1, 1).GetString());
        Assert.Equal("Ubigeo", hoja.Cell(1, ColumnasEstablecimientos.Todas.Count).GetString());
    }

    [Fact]
    public async Task Importar_CreaLosNuevosYActualizaLosExistentes()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var (_, idExistente, codigoExistente) = await CrearEnHuantaAsync(superAdmin);
        var idNuevo = Numero();
        var codigoNuevo = Numero();

        // Columnas en otro orden que la plantilla y un ubigeo numérico (Excel le quitó el 0 inicial).
        var excel = Excel(
            ["Nombre", "Red", "Id_Establecimiento", "Codigo_Unico", "Ubigeo", "Categoria"],
            ["Puesto de Salud Importado", "Red de Salud Puquio", idNuevo, codigoNuevo, 50601, "I-1"],
            ["Nombre Actualizado por Excel", "HUANTA", idExistente, codigoExistente, "050601", "I-2"]);

        var resultado = await ImportarAsync(superAdmin, excel);
        var nuevo = Assert.Single(await BuscarAsync(superAdmin, codigoNuevo));
        var existente = Assert.Single(await BuscarAsync(superAdmin, codigoExistente));

        Assert.True(resultado.Guardado);
        Assert.Equal(1, resultado.Creados);
        Assert.Equal(1, resultado.Actualizados);
        Assert.Empty(resultado.Errores);
        Assert.Equal(api.SedePuquioId, nuevo.SedeId);
        Assert.Equal("050601", nuevo.Ubigeo);
        Assert.Equal("Nombre Actualizado por Excel", existente.Nombre);
    }

    [Fact]
    public async Task Importar_ConUnaFilaConErrores_NoGuardaNada()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var codigoValido = Numero();

        var excel = Excel(
            ["Id_Establecimiento", "Codigo_Unico", "Nombre", "Red"],
            [Numero(), codigoValido, "Puesto Válido", "Huanta"],
            [Numero(), Numero(), "Puesto de Otra Región", "Red de Salud Lima"],
            [Numero(), "ABC", "Puesto con Código Malo", "Huanta"]);

        var resultado = await ImportarAsync(superAdmin, excel);

        Assert.False(resultado.Guardado);
        Assert.Equal(new[] { 3, 4 }, resultado.Errores.Select(e => e.Fila).Distinct().Order().ToArray());
        Assert.Empty(await BuscarAsync(superAdmin, codigoValido));
    }

    [Fact]
    public async Task Importar_NoPermiteCambiarDeRedNiRepetirCodigos()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var (_, idExistente, codigoExistente) = await CrearEnHuantaAsync(superAdmin);
        var codigoRepetido = Numero();

        var excel = Excel(
            ["Id_Establecimiento", "Codigo_Unico", "Nombre", "Red"],
            [idExistente, codigoExistente, "Mismo Puesto", "Puquio"],
            [Numero(), codigoRepetido, "Primero", "Huanta"],
            [Numero(), codigoRepetido, "Segundo", "Huanta"]);

        var resultado = await ImportarAsync(superAdmin, excel);

        Assert.False(resultado.Guardado);
        Assert.Contains(resultado.Errores, e => e.Fila == 2 && e.Mensaje.Contains("otra red", StringComparison.Ordinal));
        Assert.Contains(resultado.Errores, e => e.Fila == 4 && e.Mensaje.Contains("se repite", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Importar_UnArchivoQueNoEsExcel_Responde400()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        using var contenido = Archivo("esto no es un Excel"u8.ToArray());

        var respuesta = await superAdmin.PostAsync("/api/establecimientos/importar", contenido, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("Establecimientos.ArchivoInvalido", await respuesta.LeerCodigoAsync());
    }

    private static async Task<ResultadoImportacionDto> ImportarAsync(HttpClient cliente, byte[] excel)
    {
        using var contenido = Archivo(excel);
        var respuesta = await cliente.PostAsync("/api/establecimientos/importar", contenido, Ct);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<ResultadoImportacionDto>(Ct)
            ?? throw new InvalidOperationException("La API no devolvió el resultado de la importación.");
    }

    private static MultipartFormDataContent Archivo(byte[] bytes)
    {
        var archivo = new ByteArrayContent(bytes);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(TipoExcel);
        return new MultipartFormDataContent { { archivo, "archivo", "establecimientos.xlsx" } };
    }

    /// <summary>Arma un Excel en memoria: la primera fila son los encabezados.</summary>
    private static byte[] Excel(string[] encabezados, params object[][] filas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Hoja1");

        for (var c = 0; c < encabezados.Length; c++)
        {
            hoja.Cell(1, c + 1).Value = encabezados[c];
        }

        for (var f = 0; f < filas.Length; f++)
        {
            for (var c = 0; c < filas[f].Length; c++)
            {
                var celda = hoja.Cell(f + 2, c + 1);
                switch (filas[f][c])
                {
                    case int numero:
                        celda.Value = numero;
                        break;
                    case string texto:
                        celda.Value = texto;
                        break;
                }
            }
        }

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }
}
