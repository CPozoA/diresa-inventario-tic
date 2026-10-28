using Inventario.Domain.Personal;

namespace Inventario.Application.Common.Reniec;

/// <summary>Resultado de consultar un DNI.</summary>
public enum EstadoConsultaDni
{
    /// <summary>RENIEC devolvió los nombres de ese DNI.</summary>
    Encontrado,

    /// <summary>RENIEC no tiene ese DNI (o devolvió otro documento, que no se usa).</summary>
    NoEncontrado,

    /// <summary>El servicio no respondió, tardó demasiado o no está configurado.</summary>
    NoDisponible,
}

/// <summary>Lo que devolvió la consulta: los nombres solo cuando se encontró el DNI.</summary>
/// <param name="Estado">Resultado.</param>
/// <param name="Nombres">Nombres y apellidos, ya en mayúsculas; nulos si no se encontró.</param>
public sealed record ResultadoConsultaDni(EstadoConsultaDni Estado, NombresPersona? Nombres)
{
    public static ResultadoConsultaDni NoEncontrado { get; } = new(EstadoConsultaDni.NoEncontrado, null);

    public static ResultadoConsultaDni NoDisponible { get; } = new(EstadoConsultaDni.NoDisponible, null);

    public static ResultadoConsultaDni Encontrado(NombresPersona nombres) => new(EstadoConsultaDni.Encontrado, nombres);
}

/// <summary>
/// Consulta de DNI en RENIEC a través de un servicio externo (docs/ARQUITECTURA.md D50). Solo devuelve nombres y
/// apellidos: el sistema no guarda otros datos personales. Nunca lanza por fallas del servicio: responde
/// <see cref="EstadoConsultaDni.NoDisponible"/> para que se puedan escribir los nombres a mano.
/// </summary>
public interface IConsultaDni
{
    /// <param name="dni">8 dígitos, ya validados.</param>
    Task<ResultadoConsultaDni> ConsultarAsync(string dni, CancellationToken cancellationToken);
}
