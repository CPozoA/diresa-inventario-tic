using Inventario.Application.Common.Persistencia;
using Inventario.Infrastructure.Persistencia;
using Inventario.Infrastructure.Persistencia.Inicializacion;
using Inventario.Infrastructure.Persistencia.Interceptores;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Infrastructure;

public static class DependencyInjection
{
    private const string _nombreCadenaConexion = "Inventario";

    /// <summary>
    /// Registra los servicios de infraestructura: base de datos, auditoría, datos iniciales y protección de datos.
    /// Requiere que la capa Api registre <c>IUsuarioActual</c>. En el Paso 3 se agregan Identity y el correo.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Reloj inyectable: permite probar reglas que dependen de la fecha (garantías, vida útil, bloqueos).
        services.AddSingleton(TimeProvider.System);

        var cadenaConexion = configuration.GetConnectionString(_nombreCadenaConexion);
        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{_nombreCadenaConexion}'. Ver back/README.md.");
        }

        services.Configure<OpcionesBaseDatos>(configuration.GetSection(OpcionesBaseDatos.Seccion));

        services.AddScoped<AuditoriaInterceptor>();

        services.AddDbContext<AppDbContext>((proveedor, opciones) => opciones
            .UseSqlServer(cadenaConexion)
            .AddInterceptors(proveedor.GetRequiredService<AuditoriaInterceptor>()));

        services.AddScoped<IAppDbContext>(proveedor => proveedor.GetRequiredService<AppDbContext>());

        services.AddScoped<InicializadorDatos>();

        // Las claves que cifran la cookie de sesión se guardan en la base de datos (docs/ARQUITECTURA.md §6.1).
        services.AddDataProtection()
            .SetApplicationName("InventarioTIC")
            .PersistKeysToDbContext<AppDbContext>();

        return services;
    }
}
