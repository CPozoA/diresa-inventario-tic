using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Activos.Consultar;
using Inventario.Application.Catalogos;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;

namespace Inventario.Api.IntegrationTests;

/// <summary>
/// Activos (docs/ARQUITECTURA.md §5.10): ficha completa con discos y RAM, código patrimonial único por sede,
/// referencias de la misma red, permisos por acción y aislamiento entre sedes.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class ActivosTests(FabricaApi api)
{
    /// <summary>La API envía las listas fijas como texto ("Bueno", "Nvme").</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string CodigoUnico(string prefijo = "PAT") => $"{prefijo}-{Guid.NewGuid().ToString("N")[..10]}".ToUpperInvariant();

    private static int Numero() => Random.Shared.Next(10_000_000, 99_999_999);

    /// <summary>El mismo texto en minúsculas (para comprobar que la API no distingue mayúsculas).</summary>
    private static string EnMinusculas(string texto) => new(texto.Select(char.ToLowerInvariant).ToArray());

    /// <summary>Establecimiento y área de cada red, y elementos de los catálogos generales, para armar fichas.</summary>
    private sealed record Referencias(
        Guid EstablecimientoHuanta,
        Guid AreaHuanta,
        Guid EstablecimientoPuquio,
        Guid AreaPuquio,
        Guid Tipo,
        Guid Marca,
        Guid Estado,
        Guid SistemaOperativo,
        Guid Procesador,
        Guid Antivirus);

    private async Task<Referencias> PrepararAsync()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        async Task<Guid> Establecimiento(Guid sedeId)
        {
            var respuesta = await superAdmin.PostAsJsonAsync(
                "/api/establecimientos",
                new { sedeId, idEstablecimiento = Numero(), codigoUnico = Numero(), nombre = "Puesto de Salud de Activos" },
                Ct);
            respuesta.EnsureSuccessStatusCode();
            return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
        }

        async Task<Guid> Area(Guid sedeId)
        {
            var respuesta = await superAdmin.PostAsJsonAsync("/api/catalogos/areas", new { sedeId, nombre = CodigoUnico("Área") }, Ct);
            respuesta.EnsureSuccessStatusCode();
            return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
        }

        async Task<Guid> Elemento(string catalogo, string nombre) =>
            (await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>($"/api/catalogos/{catalogo}", Json, Ct))!
                .Single(e => e.Nombre == nombre).Id;

        return new Referencias(
            await Establecimiento(api.SedeHuantaId),
            await Area(api.SedeHuantaId),
            await Establecimiento(api.SedePuquioId),
            await Area(api.SedePuquioId),
            await Elemento("tipos-activo", "Laptop"),
            await Elemento("marcas", "Dell"),
            await Elemento("estados-activo", "Operativo"),
            await Elemento("sistemas-operativos", "Windows 11 Pro"),
            await Elemento("procesadores", "Intel Core i7"),
            await Elemento("antivirus", "Microsoft Defender"));
    }

    /// <summary>Ficha como la enviaría la interfaz.</summary>
    private static object Ficha(
        Referencias r,
        string codigo,
        Guid? establecimiento = null,
        Guid? area = null,
        string? serie = null,
        string fechaAdquisicion = "2023-03-15",
        string finGarantia = "2024-03-14",
        object[]? modulosRam = null,
        string ip = "192.168.10.45",
        string nombreEquipo = "ADM-LT-001",
        string modelo = "Latitude 5440") =>
        new
        {
            establecimientoId = establecimiento ?? r.EstablecimientoHuanta,
            generales = new
            {
                codigoPatrimonial = codigo,
                tipoActivoId = r.Tipo,
                marcaId = r.Marca,
                modelo,
                numeroSerie = serie,
                areaId = area ?? r.AreaHuanta,
                ubicacionFisica = "Piso 2, oficina 204",
                responsable = "Juan Pérez Quispe",
                estadoActivoId = r.Estado,
                conservacion = "Bueno",
                criticidad = "Media",
            },
            adquisicion = new
            {
                fechaAdquisicion,
                proveedor = "Proveedor Tecnológico S.A.C.",
                tipoAdquisicion = "Compra",
                valorAdquisicion = 3850.00m,
                vidaUtilAnios = 4,
                finGarantia,
            },
            tecnicos = new
            {
                procesadorId = r.Procesador,
                generacionProcesador = "13",
                sistemaOperativoId = r.SistemaOperativo,
                nombreEquipo,
                direccionIp = ip,
                direccionMac = "a4-bb-6d-12-34-56",
                discoCifrado = true,
                antivirusId = r.Antivirus,
                tipoGrafica = "Dedicada",
                modeloGrafica = "NVIDIA T550",
                puertos = new[] { "Hdmi", "UsbA", "UsbA" },
                accesorios = new[] { "Cargador" },
            },
            discos = new object[]
            {
                new { ranura = "M.2 #1", tipo = "Nvme", capacidadGb = 512, marcaModelo = "Samsung PM9B1", uso = "Sistema" },
                new { ranura = "M.2 #2", tipo = "SsdSata", capacidadGb = 1024, uso = "Datos" },
            },
            modulosRam = modulosRam ?? new object[]
            {
                new { ranura = "A", tipo = "Ddr5", capacidadGb = 8, velocidadMhz = 5600 },
                new { ranura = "B", tipo = "Ddr5", capacidadGb = 8, velocidadMhz = 5600 },
            },
        };

    private static async Task<Guid> CrearAsync(HttpClient cliente, object ficha)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/activos", ficha, Ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
    }

    private static Task<FichaActivoDto?> LeerFichaAsync(HttpClient cliente, Guid id) =>
        cliente.GetFromJsonAsync<FichaActivoDto>($"/api/activos/{id}", Json, Ct);

    [Fact]
    public async Task Editor_RegistraUnaFichaCompleta_YLaLeeConLosDatosCalculados()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var codigo = CodigoUnico();

        var id = await CrearAsync(editor, Ficha(r, EnMinusculas(codigo)));
        var ficha = await LeerFichaAsync(editor, id);

        Assert.NotNull(ficha);
        Assert.Equal(api.SedeHuantaId, ficha.SedeId);
        Assert.Equal(codigo, ficha.Generales.CodigoPatrimonial);
        Assert.Equal("Dell", ficha.Generales.Marca.Nombre);
        Assert.Equal("Windows 11 Pro", ficha.Tecnicos.SistemaOperativo?.Nombre);
        Assert.Equal("Intel Core i7", ficha.Tecnicos.Procesador?.Nombre);
        Assert.Equal("13.ª generación", ficha.Tecnicos.Generacion?.Texto);
        Assert.Equal("Microsoft Defender", ficha.Tecnicos.Antivirus?.Nombre);
        Assert.Equal(TipoGrafica.Dedicada, ficha.Tecnicos.TipoGrafica);
        Assert.Equal(new[] { "USB-A", "HDMI" }, ficha.Tecnicos.Puertos.Select(p => p.Texto));
        Assert.Equal("Cargador", Assert.Single(ficha.Tecnicos.Accesorios).Valor);
        Assert.Equal("A4:BB:6D:12:34:56", ficha.Tecnicos.DireccionMac);
        Assert.Equal(1536, ficha.AlmacenamientoTotalGb);
        Assert.Equal(16, ficha.RamTotalGb);
        Assert.Equal(TipoDisco.Nvme, ficha.Discos[0].Tipo);
        Assert.Equal(EstadoGarantia.Vencida, ficha.Situacion.Garantia);
    }

    [Fact]
    public async Task CodigoRepetidoEnLaSede_Responde409_PeroEnOtraSedeSePermite()
    {
        var r = await PrepararAsync();
        using var huanta = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var puquio = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);
        var codigo = CodigoUnico();

        await CrearAsync(huanta, Ficha(r, codigo));
        var repetido = await huanta.PostAsJsonAsync("/api/activos", Ficha(r, $"  {EnMinusculas(codigo)} "), Ct);
        var enPuquio = await puquio.PostAsJsonAsync("/api/activos", Ficha(r, codigo, r.EstablecimientoPuquio, r.AreaPuquio), Ct);

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal("Activos.CodigoDuplicado", await repetido.LeerCodigoAsync());
        Assert.Equal(HttpStatusCode.Created, enPuquio.StatusCode);
    }

    [Fact]
    public async Task UsuarioDeOtraSede_NoVeNiModificaElActivo()
    {
        var r = await PrepararAsync();
        using var huanta = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var puquio = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);
        var codigo = CodigoUnico();
        var id = await CrearAsync(huanta, Ficha(r, codigo));

        var ver = await puquio.GetAsync($"/api/activos/{id}", Ct);
        var editar = await puquio.PutAsJsonAsync($"/api/activos/{id}", Ficha(r, codigo, r.EstablecimientoPuquio, r.AreaPuquio), Ct);
        var lista = await puquio.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?buscar={codigo}", Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, ver.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, editar.StatusCode);
        Assert.Equal(0, lista!.Total);
    }

    [Fact]
    public async Task EstablecimientoOAreaDeOtraRed_SeRechazan()
    {
        var r = await PrepararAsync();
        using var huanta = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var establecimiento = await huanta.PostAsJsonAsync("/api/activos", Ficha(r, CodigoUnico(), r.EstablecimientoPuquio), Ct);
        var area = await huanta.PostAsJsonAsync("/api/activos", Ficha(r, CodigoUnico(), area: r.AreaPuquio), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, establecimiento.StatusCode);
        Assert.Equal("Activos.EstablecimientoInvalido", await establecimiento.LeerCodigoAsync());
        Assert.Equal(HttpStatusCode.BadRequest, area.StatusCode);
        Assert.Equal("Activos.ReferenciaInvalida", await area.LeerCodigoAsync());
    }

    [Fact]
    public async Task Editar_ReemplazaLaRam_PeroNoCambiaDeRed()
    {
        var r = await PrepararAsync();
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var codigo = CodigoUnico();
        var id = await CrearAsync(superAdmin, Ficha(r, codigo));

        var otraRed = await superAdmin.PutAsJsonAsync($"/api/activos/{id}", Ficha(r, codigo, r.EstablecimientoPuquio, r.AreaPuquio), Ct);
        var editar = await superAdmin.PutAsJsonAsync(
            $"/api/activos/{id}",
            Ficha(r, codigo, modulosRam: [new { tipo = "Ddr5", capacidadGb = 32, velocidadMhz = 5600 }]),
            Ct);
        var ficha = await LeerFichaAsync(superAdmin, id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, otraRed.StatusCode);
        Assert.Equal("Activos.OtraRed", await otraRed.LeerCodigoAsync());
        Assert.Equal(HttpStatusCode.NoContent, editar.StatusCode);
        Assert.Equal(32, ficha!.RamTotalGb);
        Assert.Single(ficha.ModulosRam);
        Assert.Equal(api.SedeHuantaId, ficha.SedeId);
    }

    [Fact]
    public async Task SoloQuienTieneElPermisoEliminaYRegistra()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);
        var id = await CrearAsync(editor, Ficha(r, CodigoUnico()));

        var delGestor = await gestor.PostAsJsonAsync("/api/activos", Ficha(r, CodigoUnico()), Ct);
        var delEditor = await editor.DeleteAsync($"/api/activos/{id}", Ct);
        var delAdmin = await admin.DeleteAsync($"/api/activos/{id}", Ct);
        var despues = await admin.GetAsync($"/api/activos/{id}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, delGestor.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delEditor.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, despues.StatusCode);
    }

    [Fact]
    public async Task Listar_FiltraPorBusquedaYGarantia_YPagina()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var prefijo = CodigoUnico("LST");
        await CrearAsync(editor, Ficha(r, $"{prefijo}-1"));
        await CrearAsync(editor, Ficha(r, $"{prefijo}-2"));
        await CrearAsync(editor, Ficha(r, $"{prefijo}-3", finGarantia: "2099-12-31"));

        var primera = await editor.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?buscar={prefijo}&tamanoPagina=2", Json, Ct);
        var vigentes = await editor.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?buscar={prefijo}&garantia=Vigente", Json, Ct);

        Assert.Equal(3, primera!.Total);
        Assert.Equal(2, primera.Activos.Count);
        Assert.Equal($"{prefijo}-1", primera.Activos[0].CodigoPatrimonial);
        Assert.Equal($"{prefijo}-3", Assert.Single(vigentes!.Activos).CodigoPatrimonial);
    }

    [Fact]
    public async Task Buscar_EncuentraPorIpYPorNombreDeEquipo()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var ip = $"10.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}";
        var equipo = CodigoUnico("EQ");
        var porIp = await CrearAsync(editor, Ficha(r, CodigoUnico(), ip: ip));
        var porEquipo = await CrearAsync(editor, Ficha(r, CodigoUnico(), nombreEquipo: equipo));

        var conIp = await editor.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?buscar={ip}", Json, Ct);
        var conEquipo = await editor.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?buscar={EnMinusculas(equipo)}", Json, Ct);

        Assert.Equal(porIp, Assert.Single(conIp!.Activos).Id);
        Assert.Equal(porEquipo, Assert.Single(conEquipo!.Activos).Id);
    }

    [Fact]
    public async Task Sugerencias_DevuelvenLoYaRegistradoEnLaRed()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var puquio = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);
        var modelo = CodigoUnico("Latitude");
        await CrearAsync(editor, Ficha(r, CodigoUnico(), modelo: modelo));

        var modelos = await editor.GetFromJsonAsync<List<string>>(
            $"/api/activos/sugerencias?campo=Modelo&texto={EnMinusculas(modelo)}&marcaId={r.Marca}", Json, Ct);
        var proveedores = await editor.GetFromJsonAsync<List<string>>("/api/activos/sugerencias?campo=Proveedor&texto=tecno", Json, Ct);
        var discos = await editor.GetFromJsonAsync<List<string>>("/api/activos/sugerencias?campo=MarcaModeloDisco&texto=samsung", Json, Ct);
        var deOtraRed = await puquio.GetFromJsonAsync<List<string>>($"/api/activos/sugerencias?campo=Modelo&texto={modelo}", Json, Ct);

        Assert.Equal(modelo, Assert.Single(modelos!));
        Assert.Contains("Proveedor Tecnológico S.A.C.", proveedores!);
        Assert.Contains("Samsung PM9B1", discos!);
        Assert.Empty(deOtraRed!);
    }

    [Fact]
    public async Task UnaImpresoraNoLlevaPuertosNiAntivirus()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var impresora = await TipoAsync(editor, "Impresora");

        var conPuertos = await editor.PostAsJsonAsync("/api/activos", FichaDeTipo(r, impresora, new { puertos = new[] { "UsbA" } }), Ct);
        var conAntivirus = await editor.PostAsJsonAsync("/api/activos", FichaDeTipo(r, impresora, new { antivirusId = r.Antivirus }), Ct);

        Assert.Equal("Activos.DatosNoCorresponden", await conPuertos.LeerCodigoAsync());
        Assert.Equal("Activos.DatosNoCorresponden", await conAntivirus.LeerCodigoAsync());
    }

    [Fact]
    public async Task LaGeneracionDebeSerDeLaFamiliaDelProcesador()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var conTecnicos = (object tecnicos) => FichaDeTipo(r, r.Tipo, tecnicos);

        // Intel Core i7 ofrece de la 1.ª a la 14.ª generación; "5000" es de AMD Ryzen.
        var deOtraFamilia = await editor.PostAsJsonAsync(
            "/api/activos", conTecnicos(new { procesadorId = r.Procesador, generacionProcesador = "5000" }), Ct);
        var sinProcesador = await editor.PostAsJsonAsync("/api/activos", conTecnicos(new { generacionProcesador = "13" }), Ct);
        var correcta = await editor.PostAsJsonAsync(
            "/api/activos", conTecnicos(new { procesadorId = r.Procesador, generacionProcesador = "12" }), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, deOtraFamilia.StatusCode);
        Assert.Equal("Activos.GeneracionInvalida", await deOtraFamilia.LeerCodigoAsync());
        Assert.Equal(HttpStatusCode.BadRequest, sinProcesador.StatusCode);
        Assert.Equal(HttpStatusCode.Created, correcta.StatusCode);
    }

    [Fact]
    public async Task SoloElSuperAdminCambiaLasGeneracionesDeUnProcesador()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var crear = await superAdmin.PostAsJsonAsync("/api/catalogos/procesadores", new { nombre = CodigoUnico("CPU") }, Ct);
        var id = (await crear.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;

        var delAdmin = await admin.PutAsJsonAsync($"/api/catalogos/procesadores/{id}/generaciones", new { generaciones = "AmdRyzen" }, Ct);
        var delSuper = await superAdmin.PutAsJsonAsync($"/api/catalogos/procesadores/{id}/generaciones", new { generaciones = "AmdRyzen" }, Ct);
        var procesadores = (await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>("/api/catalogos/procesadores", Json, Ct))!;
        var listas = await superAdmin.GetFromJsonAsync<ListasActivoDto>("/api/activos/listas", Json, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, delAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delSuper.StatusCode);
        Assert.Equal(GeneracionesProcesador.AmdRyzen, procesadores.Single(p => p.Id == id).Generaciones);
        Assert.Equal(GeneracionesProcesador.IntelCoreUltra, procesadores.Single(p => p.Nombre == "Intel Core Ultra 7").Generaciones);
        Assert.Equal(14, listas!.GeneracionesProcesador.Single(g => g.Familia == "IntelCore").Opciones.Count);
    }

    [Fact]
    public async Task SerieRepetida_SeAvisaSinDistinguirMayusculas()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var serie = CodigoUnico("SER");
        var codigo = CodigoUnico();
        await CrearAsync(editor, Ficha(r, codigo, serie: serie));

        var coincidencias = await editor.GetFromJsonAsync<List<ActivoConSerieDto>>(
            $"/api/activos/serie?numero={EnMinusculas(serie)}",
            Json,
            Ct);

        Assert.Equal(codigo, Assert.Single(coincidencias!).CodigoPatrimonial);
    }

    [Fact]
    public async Task RamConVelocidadQueNoEsDeSuTipo_O_FechaFutura_Responden400()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var ram = await editor.PostAsJsonAsync(
            "/api/activos",
            Ficha(r, CodigoUnico(), modulosRam: [new { tipo = "Ddr4", capacidadGb = 8, velocidadMhz = 5600 }]),
            Ct);
        var futura = await editor.PostAsJsonAsync("/api/activos", Ficha(r, CodigoUnico(), fechaAdquisicion: "2999-01-01"), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, ram.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, futura.StatusCode);
        Assert.Equal("Activos.FechaFutura", await futura.LeerCodigoAsync());
    }

    /// <summary>Ficha mínima de un tipo cualquiera, con los datos técnicos y discos indicados.</summary>
    private static object FichaDeTipo(Referencias r, Guid tipoId, object? tecnicos, object[]? discos = null) =>
        new
        {
            establecimientoId = r.EstablecimientoHuanta,
            generales = new
            {
                codigoPatrimonial = CodigoUnico(),
                tipoActivoId = tipoId,
                marcaId = r.Marca,
                modelo = "Modelo de prueba",
                areaId = r.AreaHuanta,
                estadoActivoId = r.Estado,
                conservacion = "Nuevo",
            },
            tecnicos,
            discos = discos ?? [],
        };

    private static async Task<Guid> TipoAsync(HttpClient cliente, string nombre) =>
        (await cliente.GetFromJsonAsync<List<ElementoCatalogoDto>>("/api/catalogos/tipos-activo", Json, Ct))!
            .Single(t => t.Nombre == nombre).Id;

    [Fact]
    public async Task SoloSeAceptanLosDatosTecnicosQueLlevaElTipo()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var monitor = await TipoAsync(editor, "Monitor");
        var impresora = await TipoAsync(editor, "Impresora");

        var monitorConDisco = await editor.PostAsJsonAsync(
            "/api/activos",
            FichaDeTipo(r, monitor, null, [new { tipo = "Hdd", capacidadGb = 500, uso = "Datos" }]),
            Ct);
        var impresoraConIp = await editor.PostAsJsonAsync(
            "/api/activos",
            FichaDeTipo(r, impresora, new { direccionIp = "192.168.10.60", direccionMac = "A4BB6D123456" }),
            Ct);
        var impresoraConNucleos = await editor.PostAsJsonAsync(
            "/api/activos",
            FichaDeTipo(r, impresora, new { nucleos = 4 }),
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, monitorConDisco.StatusCode);
        Assert.Equal("Activos.DatosNoCorresponden", await monitorConDisco.LeerCodigoAsync());
        Assert.Equal(HttpStatusCode.Created, impresoraConIp.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, impresoraConNucleos.StatusCode);

        var id = (await impresoraConIp.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
        Assert.Equal(FichaTecnica.Red, (await LeerFichaAsync(editor, id))!.Ficha);
    }

    [Fact]
    public async Task SoloElSuperAdminCambiaLaFichaDeUnTipo()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        using var admin = await api.CrearClienteConSesionAsync(DatosPrueba.AdminHuanta);
        var crear = await superAdmin.PostAsJsonAsync("/api/catalogos/tipos-activo", new { nombre = CodigoUnico("Plotter") }, Ct);
        var id = (await crear.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;

        var delAdmin = await admin.PutAsJsonAsync($"/api/catalogos/tipos-activo/{id}/ficha", new { ficha = "Red" }, Ct);
        var delSuper = await superAdmin.PutAsJsonAsync($"/api/catalogos/tipos-activo/{id}/ficha", new { ficha = "Red" }, Ct);
        var tipos = (await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>("/api/catalogos/tipos-activo", Json, Ct))!;

        Assert.Equal(HttpStatusCode.Forbidden, delAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delSuper.StatusCode);
        Assert.Equal(FichaTecnica.Red, tipos.Single(t => t.Id == id).Ficha);
        Assert.Equal(FichaTecnica.Computo, tipos.Single(t => t.Nombre == "Laptop").Ficha);
    }

    [Fact]
    public async Task Listas_TraenLosTiposYCapacidades()
    {
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);

        var listas = await editor.GetFromJsonAsync<ListasActivoDto>("/api/activos/listas", Json, Ct);

        Assert.Contains(listas!.TiposDisco, o => o.Valor == "Nvme" && o.Texto == "NVMe");
        Assert.Contains(512, listas.CapacidadesDiscoGb);
        Assert.Contains(listas.VelocidadesRam, v => v.Tipo == "Ddr4" && v.VelocidadesMhz.Contains(3200));
    }

    [Fact]
    public async Task Exportar_DaUnExcelConActivosDiscosYRam_ConLosFiltrosDeLaLista()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var codigo = CodigoUnico();
        await CrearAsync(editor, Ficha(r, codigo));

        var respuesta = await editor.GetAsync($"/api/activos/exportar?buscar={codigo}", Ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", respuesta.Content.Headers.ContentType?.MediaType);
        var nombre = (respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName ?? string.Empty).Trim('"');
        Assert.StartsWith("Inventario-", nombre, StringComparison.Ordinal);
        Assert.EndsWith(".xlsx", nombre, StringComparison.Ordinal);

        using (var libro = new XLWorkbook(await respuesta.Content.ReadAsStreamAsync(Ct)))
        {
            var activos = libro.Worksheet("Activos");
            Assert.Equal("Código patrimonial", activos.Cell(1, 1).GetString());
            Assert.Equal(codigo, activos.Cell(2, 1).GetString());
            Assert.True(activos.Cell(3, 1).IsEmpty());

            // Una fila por disco y por módulo, más el encabezado.
            Assert.Equal(3, libro.Worksheet("Discos").RowsUsed().Count());
            Assert.Equal(3, libro.Worksheet("Memoria RAM").RowsUsed().Count());
            Assert.Equal("Información", libro.Worksheet("Información").Name);
        }

        // El superadministrador con todas las redes recibe la columna "Red".
        var deSuper = await superAdmin.GetAsync($"/api/activos/exportar?buscar={codigo}", Ct);
        using var libroSuper = new XLWorkbook(await deSuper.Content.ReadAsStreamAsync(Ct));
        Assert.Equal("Red", libroSuper.Worksheet("Activos").Cell(1, 1).GetString());
        Assert.Equal(codigo, libroSuper.Worksheet("Activos").Cell(2, 2).GetString());
    }
}
