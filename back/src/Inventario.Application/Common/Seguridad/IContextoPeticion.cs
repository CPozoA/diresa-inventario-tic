namespace Inventario.Application.Common.Seguridad;

/// <summary>Datos de la petición HTTP que no son del usuario: por ahora, la dirección IP (para la auditoría).</summary>
public interface IContextoPeticion
{
    /// <summary>Dirección IP de origen; nula fuera de una petición HTTP.</summary>
    string? Ip { get; }
}
