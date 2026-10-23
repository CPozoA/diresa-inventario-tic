using System.Net.Http.Json;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Seguridad;
using Inventario.Domain.Usuarios;
using Inventario.Infrastructure.Identidad;
using Inventario.Infrastructure.Persistencia;
using Inventario.Infrastructure.Persistencia.Interceptores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Api.IntegrationTests.Infraestructura;

/// <summary>
/// Levanta la API en memoria contra la base de pruebas. Una sola vez por ejecución:
/// borra la base, la API aplica las migraciones y carga los datos iniciales, y luego se crean los usuarios de prueba.
/// </summary>
public sealed class FabricaApi : WebApplicationFactory<MarcadorApi>, IAsyncLifetime
{
    public Guid SedeHuantaId { get; private set; }

    public Guid SedePuquioId { get; private set; }

    public async ValueTask InitializeAsync()
    {
        await using (var contexto = CrearContexto(UsuarioFalso.ProcesoInterno()))
        {
            await contexto.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }

        // Arranca la API: aplica migraciones y carga sedes, roles y superadministrador.
        _ = Server;

        await CrearDatosDePruebaAsync();
    }

    /// <summary>Cliente HTTP con su propio "navegador" (cookies), sin sesión iniciada.</summary>
    public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false,
    });

    /// <summary>Cliente HTTP con la sesión del usuario indicado ya iniciada.</summary>
    public async Task<HttpClient> CrearClienteConSesionAsync(string usuario, string contrasena = DatosPrueba.Contrasena)
    {
        var cliente = CrearCliente();
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/cuenta/iniciar-sesion",
            new { usuario, contrasena },
            TestContext.Current.CancellationToken);

        respuesta.EnsureSuccessStatusCode();
        return cliente;
    }

    /// <summary>
    /// Contexto de EF Core "como si" el usuario indicado hiciera la petición, con el filtro y la protección de sede reales.
    /// </summary>
    public static AppDbContext CrearContexto(IUsuarioActual usuario)
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(DatosPrueba.CadenaConexion)
            .AddInterceptors(
                new ProteccionSedeInterceptor(usuario),
                new AuditoriaInterceptor(usuario, TimeProvider.System))
            .Options;

        return new AppDbContext(opciones, usuario);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Entorno propio: no carga appsettings.Development.json ni los user secrets del desarrollador.
        builder.UseEnvironment("Pruebas");

        builder.UseSetting("ConnectionStrings:Inventario", DatosPrueba.CadenaConexion);
        builder.UseSetting("BaseDatos:AplicarMigracionesAlIniciar", "true");
        builder.UseSetting("Correo:Modo", "Carpeta");
        builder.UseSetting("Correo:Carpeta", "correos-pruebas");
        builder.UseSetting("Correo:Remitente", "pruebas@inventario.local");
        builder.UseSetting("Inicializacion:SuperAdmin:NombreUsuario", DatosPrueba.SuperAdmin);
        builder.UseSetting("Inicializacion:SuperAdmin:Correo", "superadmin@pruebas.local");
        builder.UseSetting("Inicializacion:SuperAdmin:ContrasenaInicial", DatosPrueba.Contrasena);
        builder.UseSetting("Seguridad:IntentosAutenticacionPorMinuto", "1000");
    }

    private async Task CrearDatosDePruebaAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        // Fuera de una petición HTTP: proceso interno, sin filtro de sede.
        await using var alcance = Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var cuentas = alcance.ServiceProvider.GetRequiredService<UserManager<CuentaUsuario>>();

        SedeHuantaId = await db.Sedes.Where(s => s.Codigo == DatosPrueba.CodigoHuanta).Select(s => s.Id).SingleAsync(ct);
        SedePuquioId = await db.Sedes.Where(s => s.Codigo == DatosPrueba.CodigoPuquio).Select(s => s.Id).SingleAsync(ct);

        // El superadministrador de pruebas ya no necesita cambiar su contraseña.
        var superAdmin = await cuentas.FindByNameAsync(DatosPrueba.SuperAdmin)
            ?? throw new InvalidOperationException("No se creó el superadministrador de pruebas.");
        superAdmin.DebeCambiarContrasena = false;
        await cuentas.UpdateAsync(superAdmin);

        // Un rol propio en cada sede.
        db.Roles.Add(Rol.CrearDeSede(SedeHuantaId, DatosPrueba.RolTecnico, null, [Permisos.Activos.Ver]));
        db.Roles.Add(Rol.CrearDeSede(SedePuquioId, DatosPrueba.RolTecnico, null, [Permisos.Activos.Ver]));
        await db.SaveChangesAsync(ct);

        await CrearUsuarioAsync(db, cuentas, SedeHuantaId, DatosPrueba.EditorHuanta, debeCambiar: false, ct);
        await CrearUsuarioAsync(db, cuentas, SedePuquioId, DatosPrueba.EditorPuquio, debeCambiar: false, ct);
        await CrearUsuarioAsync(db, cuentas, SedeHuantaId, DatosPrueba.TemporalHuanta, debeCambiar: true, ct);
    }

    private static async Task CrearUsuarioAsync(
        AppDbContext db,
        UserManager<CuentaUsuario> cuentas,
        Guid sedeId,
        string nombreUsuario,
        bool debeCambiar,
        CancellationToken ct)
    {
        var editor = await db.Roles.SingleAsync(r => r.SedeId == sedeId && r.Nombre == "Editor", ct);

        var usuario = Usuario.CrearDeSede(sedeId, "Usuario", nombreUsuario, "Pruebas");
        usuario.AsignarRoles([editor]);
        db.Usuarios.Add(usuario);

        var cuenta = new CuentaUsuario
        {
            Id = usuario.Id,
            UserName = nombreUsuario,
            Email = $"{nombreUsuario}@pruebas.local",
            EmailConfirmed = true,
            DebeCambiarContrasena = debeCambiar,
        };

        var resultado = await cuentas.CreateAsync(cuenta, DatosPrueba.Contrasena);
        if (!resultado.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear {nombreUsuario}: {string.Join(" ", resultado.Errors.Select(e => e.Description))}");
        }
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionApi : ICollectionFixture<FabricaApi>
{
    public const string Nombre = "API con base de pruebas";
}
