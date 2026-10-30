using Inventario.Application.Common.Seguridad;

namespace Inventario.Api.Seguridad;

/// <summary>Datos de la petición HTTP en curso (la IP de origen, para la auditoría).</summary>
internal sealed class ContextoPeticionHttp(IHttpContextAccessor accesor) : IContextoPeticion
{
    public string? Ip => accesor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
