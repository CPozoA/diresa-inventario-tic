using Inventario.Application.Common.Mensajeria;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Application;

public static class DependencyInjection
{
    private static readonly Type[] InterfacesDeHandler = [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

    /// <summary>
    /// Registra todos los handlers de comandos y consultas de este ensamblado.
    /// Al crear un handler nuevo no hace falta registrarlo a mano.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var tipos = typeof(DependencyInjection).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var tipo in tipos)
        {
            var interfaces = tipo.GetInterfaces()
                .Where(i => i.IsGenericType && InterfacesDeHandler.Contains(i.GetGenericTypeDefinition()));

            foreach (var interfaz in interfaces)
            {
                services.AddScoped(interfaz, tipo);
            }
        }

        // Servicios internos compartidos por varios handlers.
        services.AddScoped<Cuenta.ConstructorSesion>();
        services.AddScoped<Common.Seguridad.CalculadoraPermisos>();
        services.AddScoped<Common.Seguridad.IPermisosUsuarioActual, Common.Seguridad.PermisosUsuarioActual>();

        return services;
    }
}
