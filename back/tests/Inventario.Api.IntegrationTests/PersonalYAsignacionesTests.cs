using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Application.Activos.Consultar;
using Inventario.Application.Asignaciones;
using Inventario.Application.Catalogos;
using Inventario.Application.Common.Reniec;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Personal;
using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;

namespace Inventario.Api.IntegrationTests;

/// <summary>
/// Personal de cada red (D50) y asignaciones de activos (D51, D52). La consulta de DNI es la falsa de las pruebas
/// (<see cref="ConsultaDniFalsa"/>).
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class PersonalYAsignacionesTests(FabricaApi api)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    private static int Numero() => Random.Shared.Next(10_000_000, 99_999_999);

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string NombreCompleto(string dni)
    {
        var n = ConsultaDniFalsa.NombresDe(dni);
        return $"{n.Nombres} {n.ApellidoPaterno} {n.ApellidoMaterno}";
    }

    /// <summary>Establecimiento y área nuevos en cada red, y elementos de los catálogos generales.</summary>
    private sealed record Referencias(
        Guid EstablecimientoHuanta,
        Guid AreaHuanta,
        Guid EstablecimientoPuquio,
        Guid AreaPuquio,
        Guid Monitor,
        Guid Marca,
        Guid Operativo,
        Guid EnAlmacen,
        Guid DeBaja);

    private async Task<Referencias> PrepararAsync()
    {
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);

        async Task<Guid> Crear(string url, object cuerpo)
        {
            var respuesta = await superAdmin.PostAsJsonAsync(url, cuerpo, Ct);
            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
        }

        Task<Guid> Establecimiento(Guid sedeId) =>
            Crear("/api/establecimientos", new { sedeId, idEstablecimiento = Numero(), codigoUnico = Numero(), nombre = "C.S. Asignaciones" });

        Task<Guid> Area(Guid sedeId) => Crear("/api/catalogos/areas", new { sedeId, nombre = $"Área {Guid.NewGuid():N}"[..20] });

        async Task<Guid> Elemento(string catalogo, string nombre) =>
            (await superAdmin.GetFromJsonAsync<List<ElementoCatalogoDto>>($"/api/catalogos/{catalogo}", Json, Ct))!
                .Single(e => e.Nombre == nombre).Id;

        return new Referencias(
            await Establecimiento(api.SedeHuantaId),
            await Area(api.SedeHuantaId),
            await Establecimiento(api.SedePuquioId),
            await Area(api.SedePuquioId),
            await Elemento("tipos-activo", "Monitor"),
            await Elemento("marcas", "Dell"),
            await Elemento("estados-activo", "Operativo"),
            await Elemento("estados-activo", "En almacén"),
            await Elemento("estados-activo", "De baja"));
    }

    private static Task<HttpResponseMessage> PedirRegistrarAsync(
        HttpClient cliente, string dni, Guid establecimiento, Guid area, string? paterno = null, string? materno = null, string? nombres = null) =>
        cliente.PostAsJsonAsync(
            "/api/personal",
            new { dni, establecimientoId = establecimiento, areaId = area, cargo = "Asistente administrativo", apellidoPaterno = paterno, apellidoMaterno = materno, nombres },
            Ct);

    private static async Task<Guid> RegistrarAsync(
        HttpClient cliente, string dni, Guid establecimiento, Guid area, string? paterno = null, string? nombres = null)
    {
        var respuesta = await PedirRegistrarAsync(cliente, dni, establecimiento, area, paterno, null, nombres);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
    }

    private static async Task<Guid> CrearActivoAsync(HttpClient cliente, Referencias r, Guid? estado = null, Guid? establecimiento = null, Guid? area = null)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/activos",
            new
            {
                establecimientoId = establecimiento ?? r.EstablecimientoHuanta,
                generales = new
                {
                    codigoPatrimonial = $"ASG-{Guid.NewGuid():N}"[..14].ToUpperInvariant(),
                    tipoActivoId = r.Monitor,
                    marcaId = r.Marca,
                    modelo = "P2422H",
                    numeroSerie = "SN-ASG",
                    areaId = area ?? r.AreaHuanta,
                    responsable = "Responsable anterior",
                    estadoActivoId = estado ?? r.Operativo,
                    conservacion = "Bueno",
                },
                discos = Array.Empty<object>(),
            },
            Ct);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<IdCreado>(Ct))!.Id;
    }

    private static Task<HttpResponseMessage> AsignarAsync(HttpClient cliente, Guid activo, Guid persona, DateOnly fecha, string conservacion = "Bueno") =>
        cliente.PostAsJsonAsync($"/api/activos/{activo}/asignaciones", new { personaId = persona, fechaEntrega = Fecha(fecha), conservacion }, Ct);

    private static Task<HttpResponseMessage> DevolverAsync(
        HttpClient cliente, Guid activo, DateOnly fecha, Guid estado, string conservacion = "Bueno", string? observaciones = null) =>
        cliente.PostAsJsonAsync(
            $"/api/activos/{activo}/asignaciones/devolver",
            new { fecha = Fecha(fecha), conservacion, estadoActivoId = estado, observaciones },
            Ct);

    private static async Task<ConsultaDniDto> ConsultarDniAsync(HttpClient cliente, string dni)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/personal/consulta-dni", new { dni }, Ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ConsultaDniDto>(Json, Ct))!;
    }

    private static Task<PersonaDto?> LeerPersonaAsync(HttpClient cliente, Guid id) =>
        cliente.GetFromJsonAsync<PersonaDto>($"/api/personal/{id}", Json, Ct);

    private static Task<FichaActivoDto?> LeerFichaAsync(HttpClient cliente, Guid id) =>
        cliente.GetFromJsonAsync<FichaActivoDto>($"/api/activos/{id}", Json, Ct);

    private static async Task<ActivoResumenDto> EnLaListaAsync(HttpClient cliente, Guid id, string buscar) =>
        Assert.Single((await cliente.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?buscar={buscar}", Json, Ct))!.Activos, a => a.Id == id);

    [Fact]
    public async Task ConReniec_LosNombresVienenDeAlli_YElDniNoSeRepiteEnLaRed()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var dni = ConsultaDniFalsa.DniEncontrado();

        var consulta = await ConsultarDniAsync(editor, dni);
        Assert.Equal(EstadoConsultaDni.Encontrado, consulta.Reniec);
        Assert.Equal(ConsultaDniFalsa.NombresDe(dni).ApellidoPaterno, consulta.ApellidoPaterno);
        Assert.Null(consulta.Registrada);

        // Los nombres escritos se ignoran: manda RENIEC.
        var id = await RegistrarAsync(editor, dni, r.EstablecimientoHuanta, r.AreaHuanta, "Otro", "Nombre");
        var persona = await LeerPersonaAsync(editor, id);

        Assert.True(persona!.Verificada);
        Assert.Equal(NombreCompleto(dni), persona.NombreCompleto);
        Assert.Equal("Asistente administrativo", persona.Cargo);
        Assert.Equal(api.SedeHuantaId, persona.SedeId);
        Assert.Empty(persona.Equipos);

        // Ya registrada: se avisa sin volver a consultar RENIEC.
        var otraVez = await ConsultarDniAsync(editor, dni);
        Assert.Null(otraVez.Reniec);
        Assert.Equal(id, otraVez.Registrada!.Id);

        var repetida = await PedirRegistrarAsync(editor, dni, r.EstablecimientoHuanta, r.AreaHuanta);
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal("Personal.DniDuplicado", await repetida.LeerCodigoAsync());

        var invalido = await editor.PostAsJsonAsync("/api/personal/consulta-dni", new { dni = "1234" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.Equal("Personal.DniInvalido", await invalido.LeerCodigoAsync());

        // Se busca por palabras del nombre, sin importar tildes ni el orden, o por el inicio del DNI.
        var paterno = ConsultaDniFalsa.NombresDe(dni).ApellidoPaterno;
        var porNombre = await editor.GetFromJsonAsync<List<PersonaResumenDto>>($"/api/personal?buscar=quispe%20{paterno.ToLowerInvariant()}", Json, Ct);
        var porDni = await editor.GetFromJsonAsync<List<PersonaResumenDto>>($"/api/personal?buscar={dni[..5]}", Json, Ct);
        Assert.Contains(porNombre!, p => p.Id == id);
        Assert.Contains(porDni!, p => p.Id == id);
    }

    [Fact]
    public async Task SinDatosDeReniec_SeEscribenLosNombres_YLaPersonaQuedaSinVerificar()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var noEncontrado = ConsultaDniFalsa.DniNoEncontrado();

        var consulta = await ConsultarDniAsync(editor, noEncontrado);
        Assert.Equal(EstadoConsultaDni.NoEncontrado, consulta.Reniec);

        var sinNombres = await PedirRegistrarAsync(editor, noEncontrado, r.EstablecimientoHuanta, r.AreaHuanta);
        Assert.Equal(HttpStatusCode.BadRequest, sinNombres.StatusCode);
        Assert.Equal("Personal.NombresObligatorios", await sinNombres.LeerCodigoAsync());

        var id = await RegistrarAsync(editor, noEncontrado, r.EstablecimientoHuanta, r.AreaHuanta, "quispe", "ana maría");
        var persona = await LeerPersonaAsync(editor, id);
        Assert.False(persona!.Verificada);
        Assert.Equal("ANA MARÍA QUISPE", persona.NombreCompleto);

        var verificar = await editor.PostAsync($"/api/personal/{id}/verificar", null, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, verificar.StatusCode);
        Assert.Equal("Personal.DniNoEncontrado", await verificar.LeerCodigoAsync());

        // Mientras no esté verificada, los nombres se corrigen al editar.
        var editar = await editor.PutAsJsonAsync(
            $"/api/personal/{id}",
            new { establecimientoId = r.EstablecimientoHuanta, areaId = r.AreaHuanta, cargo = "Digitadora", apellidoPaterno = "Quispe", apellidoMaterno = "Ccente", nombres = "Ana" },
            Ct);
        Assert.Equal(HttpStatusCode.NoContent, editar.StatusCode);
        Assert.Equal("ANA QUISPE CCENTE", (await LeerPersonaAsync(editor, id))!.NombreCompleto);

        // RENIEC no responde: también se escriben a mano; luego se verifica cuando vuelve.
        var caido = ConsultaDniFalsa.DniNoDisponible();
        var idCaido = await RegistrarAsync(editor, caido, r.EstablecimientoHuanta, r.AreaHuanta, "Taipe", "María");
        var sinServicio = await editor.PostAsync($"/api/personal/{idCaido}/verificar", null, Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, sinServicio.StatusCode);
        Assert.Equal("Personal.ReniecNoDisponible", await sinServicio.LeerCodigoAsync());

        ConsultaDniFalsa.Forzar(caido, EstadoConsultaDni.Encontrado);
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await editor.PostAsync($"/api/personal/{idCaido}/verificar", null, Ct)).StatusCode);
        }
        finally
        {
            ConsultaDniFalsa.QuitarForzado(caido);
        }

        var verificada = await LeerPersonaAsync(editor, idCaido);
        Assert.True(verificada!.Verificada);
        Assert.Equal(NombreCompleto(caido), verificada.NombreCompleto);

        // Ya verificada, los nombres enviados al editar se ignoran.
        await editor.PutAsJsonAsync(
            $"/api/personal/{idCaido}",
            new { establecimientoId = r.EstablecimientoHuanta, areaId = r.AreaHuanta, apellidoPaterno = "Otro", nombres = "Nombre" },
            Ct);
        Assert.Equal(NombreCompleto(caido), (await LeerPersonaAsync(editor, idCaido))!.NombreCompleto);
    }

    [Fact]
    public async Task ElPersonalDeUnaRed_NoSeVeNiSeTocaDesdeOtra_YSoloEditaQuienEditaActivos()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var editorPuquio = await api.CrearClienteConSesionAsync(DatosPrueba.EditorPuquio);
        using var gestor = await api.CrearClienteConSesionAsync(DatosPrueba.GestorHuanta);
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var dni = ConsultaDniFalsa.DniEncontrado();
        var id = await RegistrarAsync(editor, dni, r.EstablecimientoHuanta, r.AreaHuanta);

        Assert.Equal(HttpStatusCode.NotFound, (await editorPuquio.GetAsync($"/api/personal/{id}", Ct)).StatusCode);
        Assert.Empty((await editorPuquio.GetFromJsonAsync<List<PersonaResumenDto>>($"/api/personal?buscar={dni}", Json, Ct))!);
        Assert.Equal(HttpStatusCode.NotFound, (await editorPuquio.PostAsync($"/api/personal/{id}/cesar", null, Ct)).StatusCode);

        var conEstablecimientoAjeno = await PedirRegistrarAsync(editorPuquio, ConsultaDniFalsa.DniEncontrado(), r.EstablecimientoHuanta, r.AreaPuquio);
        Assert.Equal("Personal.EstablecimientoInvalido", await conEstablecimientoAjeno.LeerCodigoAsync());

        var conAreaAjena = await PedirRegistrarAsync(superAdmin, ConsultaDniFalsa.DniEncontrado(), r.EstablecimientoPuquio, r.AreaHuanta);
        Assert.Equal("Personal.AreaInvalida", await conAreaAjena.LeerCodigoAsync());

        // El mismo DNI puede estar en otra red (la persona se trasladó).
        Assert.Equal(HttpStatusCode.Created, (await PedirRegistrarAsync(editorPuquio, dni, r.EstablecimientoPuquio, r.AreaPuquio)).StatusCode);

        // Solo Activos.Ver: consulta, pero no registra ni gasta consultas a RENIEC.
        Assert.Equal(HttpStatusCode.OK, (await gestor.GetAsync("/api/personal", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await gestor.PostAsJsonAsync("/api/personal/consulta-dni", new { dni }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PedirRegistrarAsync(gestor, ConsultaDniFalsa.DniEncontrado(), r.EstablecimientoHuanta, r.AreaHuanta)).StatusCode);
    }

    [Fact]
    public async Task Asignar_Reasignar_YDevolver_DejanElHistorialYElEstadoDelEquipo()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        var dniA = ConsultaDniFalsa.DniEncontrado();
        var dniB = ConsultaDniFalsa.DniEncontrado();
        var a = await RegistrarAsync(editor, dniA, r.EstablecimientoHuanta, r.AreaHuanta);
        var b = await RegistrarAsync(editor, dniB, r.EstablecimientoHuanta, r.AreaHuanta);
        var activo = await CrearActivoAsync(editor, r);
        var codigo = (await LeerFichaAsync(editor, activo))!.Generales.CodigoPatrimonial;

        // Antes de asignar, la lista muestra el responsable escrito como texto (D52).
        var antes = await EnLaListaAsync(editor, activo, codigo);
        Assert.Equal("Responsable anterior", antes.Responsable);
        Assert.False(antes.ResponsableAsignado);

        Assert.Equal(HttpStatusCode.Created, (await AsignarAsync(editor, activo, a, Hoy.AddDays(-10))).StatusCode);

        var ficha = await LeerFichaAsync(editor, activo);
        Assert.Equal(a, ficha!.Asignacion!.PersonaId);
        Assert.Equal(Hoy.AddDays(-10), ficha.Asignacion.FechaEntrega);
        Assert.Equal("Responsable anterior", ficha.Generales.Responsable);
        var asignado = await EnLaListaAsync(editor, activo, dniA);
        Assert.Equal(NombreCompleto(dniA), asignado.Responsable);
        Assert.True(asignado.ResponsableAsignado);

        var otraVez = await AsignarAsync(editor, activo, a, Hoy);
        Assert.Equal("Asignaciones.YaLaTiene", await otraVez.LeerCodigoAsync());
        var antesDeLaEntrega = await AsignarAsync(editor, activo, b, Hoy.AddDays(-11));
        Assert.Equal("Asignaciones.FechaAnterior", await antesDeLaEntrega.LeerCodigoAsync());

        // Reasignar: A lo "devuelve" el mismo día y en el mismo estado en que lo recibe B.
        Assert.Equal(HttpStatusCode.Created, (await AsignarAsync(editor, activo, b, Hoy.AddDays(-5), "Regular")).StatusCode);

        var historial = (await editor.GetFromJsonAsync<List<AsignacionActivoDto>>($"/api/activos/{activo}/asignaciones", Json, Ct))!;
        Assert.Equal([b, a], historial.Select(h => h.PersonaId));
        Assert.Null(historial[0].FechaDevolucion);
        Assert.Equal(Hoy.AddDays(-5), historial[1].FechaDevolucion);
        Assert.Equal(Conservacion.Regular, historial[1].ConservacionDevolucion);

        var deA = await LeerPersonaAsync(editor, a);
        Assert.Equal(Hoy.AddDays(-5), Assert.Single(deA!.Equipos).FechaDevolucion);
        var personal = (await editor.GetFromJsonAsync<List<PersonaResumenDto>>($"/api/personal?establecimientoId={r.EstablecimientoHuanta}", Json, Ct))!;
        Assert.Equal(0, personal.Single(p => p.Id == a).Equipos);
        Assert.Equal(1, personal.Single(p => p.Id == b).Equipos);

        // Devolver: las fechas se validan; el activo queda con el estado y la conservación de la devolución.
        Assert.Equal("Asignaciones.FechaAnterior", await (await DevolverAsync(editor, activo, Hoy.AddDays(-6), r.EnAlmacen)).LeerCodigoAsync());
        Assert.Equal("Asignaciones.FechaFutura", await (await DevolverAsync(editor, activo, Hoy.AddDays(1), r.EnAlmacen)).LeerCodigoAsync());
        var devolucion = await DevolverAsync(editor, activo, Hoy, r.EnAlmacen, "Malo", "Se devuelve sin cable");
        Assert.Equal(HttpStatusCode.NoContent, devolucion.StatusCode);

        var devuelto = await LeerFichaAsync(editor, activo);
        Assert.Null(devuelto!.Asignacion);
        Assert.Equal(r.EnAlmacen, devuelto.Generales.Estado.Id);
        Assert.Equal(GrupoEstado.EnAlmacen, devuelto.GrupoEstado);
        Assert.Equal(Conservacion.Malo, devuelto.Generales.Conservacion);
        Assert.Equal("Asignaciones.SinAsignacion", await (await DevolverAsync(editor, activo, Hoy, r.EnAlmacen)).LeerCodigoAsync());

        // En almacén no le falta responsable.
        var sinResponsable = await editor.GetFromJsonAsync<PaginaActivosDto>($"/api/activos?falta=Responsable&buscar={codigo}", Json, Ct);
        Assert.Empty(sinResponsable!.Activos);

        // Al volver a entregarlo, sale del almacén.
        Assert.Equal(HttpStatusCode.Created, (await AsignarAsync(editor, activo, a, Hoy)).StatusCode);
        Assert.Equal(r.Operativo, (await LeerFichaAsync(editor, activo))!.Generales.Estado.Id);

        var movimientos = await editor.GetFromJsonAsync<List<MovimientoDto>>("/api/asignaciones/movimientos?maximo=200", Json, Ct);
        var delActivo = movimientos!.Where(m => m.ActivoId == activo).ToList();
        Assert.Equal(3, delActivo.Count(m => m.Tipo == TipoMovimiento.Entrega));
        Assert.Equal(2, delActivo.Count(m => m.Tipo == TipoMovimiento.Devolucion));
    }

    [Fact]
    public async Task SoloSeAsignaAlPersonalActivoDeLaMismaRed_YNuncaUnEquipoDeBaja()
    {
        var r = await PrepararAsync();
        using var editor = await api.CrearClienteConSesionAsync(DatosPrueba.EditorHuanta);
        using var superAdmin = await api.CrearClienteConSesionAsync(DatosPrueba.SuperAdmin);
        var activo = await CrearActivoAsync(editor, r);
        var dePuquio = await RegistrarAsync(superAdmin, ConsultaDniFalsa.DniEncontrado(), r.EstablecimientoPuquio, r.AreaPuquio);

        Assert.Equal("Asignaciones.OtraRed", await (await AsignarAsync(superAdmin, activo, dePuquio, Hoy)).LeerCodigoAsync());
        Assert.Equal("Asignaciones.PersonaInvalida", await (await AsignarAsync(editor, activo, dePuquio, Hoy)).LeerCodigoAsync());

        var persona = await RegistrarAsync(editor, ConsultaDniFalsa.DniEncontrado(), r.EstablecimientoHuanta, r.AreaHuanta);
        Assert.Equal("Asignaciones.FechaFutura", await (await AsignarAsync(editor, activo, persona, Hoy.AddDays(1))).LeerCodigoAsync());

        // Con equipos a su cargo no se marca como cesada.
        Assert.Equal(HttpStatusCode.Created, (await AsignarAsync(editor, activo, persona, Hoy)).StatusCode);
        var cesar = await editor.PostAsync($"/api/personal/{persona}/cesar", null, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cesar.StatusCode);
        Assert.Equal("Personal.TieneEquipos", await cesar.LeerCodigoAsync());

        var cesada = await RegistrarAsync(editor, ConsultaDniFalsa.DniEncontrado(), r.EstablecimientoHuanta, r.AreaHuanta);
        Assert.Equal(HttpStatusCode.NoContent, (await editor.PostAsync($"/api/personal/{cesada}/cesar", null, Ct)).StatusCode);
        var otro = await CrearActivoAsync(editor, r);
        Assert.Equal("Asignaciones.PersonaCesada", await (await AsignarAsync(editor, otro, cesada, Hoy)).LeerCodigoAsync());
        Assert.DoesNotContain(
            (await editor.GetFromJsonAsync<List<PersonaResumenDto>>($"/api/personal?establecimientoId={r.EstablecimientoHuanta}", Json, Ct))!,
            p => p.Id == cesada);

        Assert.Equal(HttpStatusCode.NoContent, (await editor.PostAsync($"/api/personal/{cesada}/reactivar", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await AsignarAsync(editor, otro, cesada, Hoy)).StatusCode);

        var deBaja = await CrearActivoAsync(editor, r, r.DeBaja);
        Assert.Equal("Asignaciones.ActivoDeBaja", await (await AsignarAsync(editor, deBaja, persona, Hoy)).LeerCodigoAsync());
    }
}
