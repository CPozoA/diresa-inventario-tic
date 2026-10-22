using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Infrastructure.Persistencia.Inicializacion;

public static class InicializacionBaseDatos
{
    /// <summary>
    /// Aplica migraciones (si está configurado) y carga los datos iniciales.
    /// No hace nada cuando lo invoca la herramienta dotnet-ef (tiempo de diseño).
    /// </summary>
    public static async Task InicializarBaseDatosAsync(this IServiceProvider servicios, CancellationToken cancellationToken = default)
    {
        if (EF.IsDesignTime)
        {
            return;
        }

        await using var alcance = servicios.CreateAsyncScope();
        var inicializador = alcance.ServiceProvider.GetRequiredService<InicializadorDatos>();
        await inicializador.EjecutarAsync(cancellationToken);
    }
}
