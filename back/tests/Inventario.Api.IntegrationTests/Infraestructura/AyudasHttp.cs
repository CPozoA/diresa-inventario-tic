using System.Net.Http.Json;
using System.Text.Json;
using Inventario.Application.Usuarios.Crear;

namespace Inventario.Api.IntegrationTests.Infraestructura;

/// <summary>Usuario creado por una prueba: nombre único y su contraseña temporal.</summary>
public sealed record UsuarioDePrueba(Guid Id, string NombreUsuario, string ContrasenaTemporal);

/// <summary>Atajos para las pruebas de gestión a través de la API.</summary>
public static class AyudasHttp
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Lee el campo <c>codigo</c> del ProblemDetails de una respuesta de error.</summary>
    public static async Task<string?> LeerCodigoAsync(this HttpResponseMessage respuesta)
    {
        ArgumentNullException.ThrowIfNull(respuesta);

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(Ct));
        return documento.RootElement.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null;
    }

    /// <summary>Nombre de usuario único: la base de pruebas se comparte entre todas las pruebas de la ejecución.</summary>
    public static string NombreUnico(string prefijo = "prueba") => $"{prefijo}.{Guid.NewGuid():N}";

    /// <summary>Pide crear un usuario. Devuelve la respuesta tal cual, para revisar el código de estado.</summary>
    public static Task<HttpResponseMessage> PedirCrearUsuarioAsync(
        this HttpClient cliente,
        Guid rolId,
        Guid? sedeId = null,
        string? nombreUsuario = null,
        string nombres = "Persona",
        string? dni = null)
    {
        ArgumentNullException.ThrowIfNull(cliente);

        var nombre = nombreUsuario ?? NombreUnico();
        return cliente.PostAsJsonAsync(
            "/api/usuarios",
            new
            {
                sedeId,
                nombreUsuario = nombre,
                correo = $"{nombre}@pruebas.local",
                dni = dni ?? ConsultaDniFalsa.DniEncontrado(),
                nombres,
                apellidos = "De Pruebas",
                cargo = "Técnico",
                rolIds = new[] { rolId },
            },
            Ct);
    }

    /// <summary>Crea un usuario y exige que salga bien.</summary>
    public static async Task<UsuarioDePrueba> CrearUsuarioAsync(this HttpClient cliente, Guid rolId, Guid? sedeId = null)
    {
        var nombre = NombreUnico();
        var respuesta = await cliente.PedirCrearUsuarioAsync(rolId, sedeId, nombre);
        respuesta.EnsureSuccessStatusCode();

        var creado = await respuesta.Content.ReadFromJsonAsync<UsuarioCreadoDto>(Ct)
            ?? throw new InvalidOperationException("La API no devolvió el usuario creado.");

        return new UsuarioDePrueba(creado.Id, nombre, creado.ContrasenaTemporal);
    }

    /// <summary>Intenta iniciar sesión con un cliente nuevo.</summary>
    public static async Task<HttpResponseMessage> IntentarIniciarSesionAsync(this FabricaApi api, string usuario, string contrasena)
    {
        ArgumentNullException.ThrowIfNull(api);

        using var cliente = api.CrearCliente();
        return await cliente.PostAsJsonAsync("/api/cuenta/iniciar-sesion", new { usuario, contrasena }, Ct);
    }
}
