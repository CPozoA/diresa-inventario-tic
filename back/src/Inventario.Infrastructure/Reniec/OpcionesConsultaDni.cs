namespace Inventario.Infrastructure.Reniec;

/// <summary>Servicio de consulta de DNI (D50). Sección <c>ConsultaDni</c> de la configuración.</summary>
/// <remarks>
/// La clave es un secreto: nunca va en appsettings ni en el repositorio. En desarrollo se guarda con
/// <c>dotnet user-secrets set "ConsultaDni:Clave" "…"</c>; en el servidor, en la variable de entorno
/// <c>ConsultaDni__Clave</c>. Sin clave, la consulta responde "no disponible" y los nombres se escriben a mano.
/// </remarks>
public sealed class OpcionesConsultaDni
{
    public const string Seccion = "ConsultaDni";

    /// <summary>Dirección del servicio (PeruDevs). El DNI y la clave se agregan como parámetros.</summary>
    public Uri Url { get; set; } = new("https://api.perudevs.com/api/v1/dni/complete");

    /// <summary>Clave del servicio (secreto).</summary>
    public string? Clave { get; set; }

    /// <summary>Si el servicio tarda más, se responde "no disponible".</summary>
    public int SegundosDeEspera { get; set; } = 8;

    /// <summary>Cuánto se recuerda un DNI encontrado (evita pagar dos veces la consulta al buscar y luego guardar).</summary>
    public int MinutosEnMemoria { get; set; } = 30;
}
