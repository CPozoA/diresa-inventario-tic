using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los servicios de infraestructura. En el Paso 2 se agregan EF Core y SQL Server;
    /// en el Paso 3, Identity y el envío de correo.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Reloj inyectable: permite probar reglas que dependen de la fecha (garantías, vida útil, bloqueos).
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
