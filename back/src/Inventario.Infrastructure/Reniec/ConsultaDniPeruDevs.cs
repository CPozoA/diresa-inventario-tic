using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inventario.Application.Common.Reniec;
using Inventario.Domain.Personal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventario.Infrastructure.Reniec;

/// <summary>
/// Consulta de DNI con el servicio de PeruDevs (D50). Solo toma nombres y apellidos de la respuesta: la fecha de
/// nacimiento, el género y el código de verificación se descartan. Comprueba que el documento devuelto sea el pedido.
/// </summary>
/// <remarks>
/// Los registros (logs) no llevan el DNI, los nombres ni la clave. El cliente HTTP se registra sin sus registros
/// automáticos, que escribirían la dirección con la clave.
/// </remarks>
internal sealed partial class ConsultaDniPeruDevs(
    HttpClient http,
    IOptions<OpcionesConsultaDni> opciones,
    IMemoryCache memoria,
    ILogger<ConsultaDniPeruDevs> logger) : IConsultaDni
{
    public async Task<ResultadoConsultaDni> ConsultarAsync(string dni, CancellationToken cancellationToken)
    {
        if (!Persona.EsDniValido(dni))
        {
            return ResultadoConsultaDni.NoEncontrado;
        }

        var clave = ClaveEnMemoria(dni);
        if (memoria.TryGetValue(clave, out NombresPersona? recordados) && recordados is not null)
        {
            return ResultadoConsultaDni.Encontrado(recordados);
        }

        var o = opciones.Value;
        if (string.IsNullOrWhiteSpace(o.Clave))
        {
            SinClave(logger);
            return ResultadoConsultaDni.NoDisponible;
        }

        var direccion = new Uri($"{o.Url}?document={dni}&key={Uri.EscapeDataString(o.Clave)}");

        RespuestaPeruDevs? respuesta;
        try
        {
            using var mensaje = await http.GetAsync(direccion, cancellationToken);

            if (mensaje.StatusCode == HttpStatusCode.NotFound)
            {
                return ResultadoConsultaDni.NoEncontrado;
            }

            if (!mensaje.IsSuccessStatusCode)
            {
                RespuestaFallida(logger, (int)mensaje.StatusCode);
                return ResultadoConsultaDni.NoDisponible;
            }

            respuesta = await mensaje.Content.ReadFromJsonAsync<RespuestaPeruDevs>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Sin conexión, respuesta ilegible o tiempo de espera agotado.
            NoResponde(logger, ex.GetType().Name);
            return ResultadoConsultaDni.NoDisponible;
        }

        var r = respuesta?.Resultado;
        if (respuesta?.Estado != true || r is null)
        {
            return ResultadoConsultaDni.NoEncontrado;
        }

        // El servicio debe devolver el mismo documento que se pidió; si no, esos datos son de otra persona.
        if (!string.Equals(r.Id?.Trim(), dni, StringComparison.Ordinal))
        {
            OtroDocumento(logger);
            return ResultadoConsultaDni.NoEncontrado;
        }

        if (string.IsNullOrWhiteSpace(r.ApellidoPaterno) || string.IsNullOrWhiteSpace(r.Nombres))
        {
            return ResultadoConsultaDni.NoEncontrado;
        }

        var nombres = new NombresPersona(
            Persona.NormalizarNombre(r.ApellidoPaterno),
            string.IsNullOrWhiteSpace(r.ApellidoMaterno) ? null : Persona.NormalizarNombre(r.ApellidoMaterno),
            Persona.NormalizarNombre(r.Nombres));

        memoria.Set(clave, nombres, TimeSpan.FromMinutes(Math.Max(1, o.MinutosEnMemoria)));
        return ResultadoConsultaDni.Encontrado(nombres);
    }

    private static string ClaveEnMemoria(string dni) => $"ConsultaDni:{dni}";

    [LoggerMessage(EventId = 1301, Level = LogLevel.Warning,
        Message = "La consulta de DNI no está configurada (falta ConsultaDni:Clave). Los nombres se escribirán a mano.")]
    private static partial void SinClave(ILogger logger);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Warning, Message = "La consulta de DNI respondió con el código HTTP {Codigo}.")]
    private static partial void RespuestaFallida(ILogger logger, int codigo);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Warning, Message = "La consulta de DNI no respondió ({Motivo}).")]
    private static partial void NoResponde(ILogger logger, string motivo);

    [LoggerMessage(EventId = 1304, Level = LogLevel.Warning,
        Message = "La consulta de DNI devolvió un documento distinto del pedido; se descartó la respuesta.")]
    private static partial void OtroDocumento(ILogger logger);

    // Solo se leen los campos que se usan; el resto de la respuesta se ignora.
    private sealed record RespuestaPeruDevs(
        [property: JsonPropertyName("estado")] bool? Estado,
        [property: JsonPropertyName("resultado")] ResultadoPeruDevs? Resultado);

    private sealed record ResultadoPeruDevs(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("nombres")] string? Nombres,
        [property: JsonPropertyName("apellido_paterno")] string? ApellidoPaterno,
        [property: JsonPropertyName("apellido_materno")] string? ApellidoMaterno);
}
