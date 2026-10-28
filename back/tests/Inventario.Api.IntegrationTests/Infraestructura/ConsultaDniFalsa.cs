using System.Collections.Concurrent;
using Inventario.Application.Common.Reniec;
using Inventario.Domain.Personal;

namespace Inventario.Api.IntegrationTests.Infraestructura;

/// <summary>
/// Reemplaza la consulta de DNI en las pruebas: nunca sale a internet. Un DNI que empieza con 8 "no se encuentra";
/// uno que empieza con 9, "no responde"; los demás se encuentran con nombres hechos a partir de sus dígitos
/// (0 → A, 1 → B…), así cada DNI tiene un apellido distinto. Se puede forzar la respuesta de un DNI.
/// </summary>
public sealed class ConsultaDniFalsa : IConsultaDni
{
    private static readonly ConcurrentDictionary<string, EstadoConsultaDni> Forzados = new();

    /// <summary>Nombres que "da RENIEC" para un DNI que se encuentra.</summary>
    public static NombresPersona NombresDe(string dni) =>
        new(new string(dni.Select(d => (char)('A' + (d - '0'))).ToArray()), "QUISPE", "JUAN");

    /// <summary>DNI al azar que se encuentra (empieza con 1 a 7).</summary>
    public static string DniEncontrado() => Random.Shared.Next(10_000_000, 79_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>DNI al azar que RENIEC no encuentra.</summary>
    public static string DniNoEncontrado() => Random.Shared.Next(80_000_000, 89_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>DNI al azar para el que RENIEC no responde.</summary>
    public static string DniNoDisponible() => Random.Shared.Next(90_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static void Forzar(string dni, EstadoConsultaDni estado) => Forzados[dni] = estado;

    public static void QuitarForzado(string dni) => Forzados.TryRemove(dni, out _);

    public Task<ResultadoConsultaDni> ConsultarAsync(string dni, CancellationToken cancellationToken)
    {
        var estado = Forzados.TryGetValue(dni, out var forzado)
            ? forzado
            : dni[0] switch
            {
                '8' => EstadoConsultaDni.NoEncontrado,
                '9' => EstadoConsultaDni.NoDisponible,
                _ => EstadoConsultaDni.Encontrado,
            };

        return Task.FromResult(estado switch
        {
            EstadoConsultaDni.Encontrado => ResultadoConsultaDni.Encontrado(NombresDe(dni)),
            EstadoConsultaDni.NoEncontrado => ResultadoConsultaDni.NoEncontrado,
            _ => ResultadoConsultaDni.NoDisponible,
        });
    }
}
