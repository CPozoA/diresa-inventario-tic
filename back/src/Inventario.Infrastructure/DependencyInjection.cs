using Inventario.Application.Common.Archivos;
using Inventario.Application.Common.Correo;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Seguridad;
using Inventario.Application.Establecimientos.Importar;
using Inventario.Infrastructure.Archivos;
using Inventario.Infrastructure.Correo;
using Inventario.Infrastructure.Enlaces;
using Inventario.Infrastructure.Excel;
using Inventario.Infrastructure.Identidad;
using Inventario.Infrastructure.Persistencia;
using Inventario.Infrastructure.Persistencia.Inicializacion;
using Inventario.Infrastructure.Persistencia.Interceptores;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventario.Infrastructure;

public static class DependencyInjection
{
    private const string NombreCadenaConexion = "Inventario";

    /// <summary>
    /// Registra los servicios de infraestructura: base de datos, auditoría, datos iniciales, protección de datos,
    /// Identity (credenciales, bloqueo, tokens) y correo. Requiere que la capa Api registre <c>IUsuarioActual</c>
    /// y la autenticación por cookie.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Reloj inyectable: permite probar reglas que dependen de la fecha (garantías, vida útil, bloqueos).
        services.AddSingleton(TimeProvider.System);

        AgregarPersistencia(services, configuration);
        AgregarIdentidad(services);
        AgregarCorreoYEnlaces(services, configuration);

        // Fotos de los activos en una carpeta del servidor (D38).
        services.Configure<OpcionesAlmacenamiento>(configuration.GetSection(OpcionesAlmacenamiento.Seccion));
        services.AddSingleton<IAlmacenArchivos, AlmacenArchivosEnDisco>();

        // Excel (ClosedXML, D42).
        services.AddSingleton<IHojaEstablecimientos, HojaEstablecimientosExcel>();

        services.Configure<OpcionesInicializacion>(configuration.GetSection(OpcionesInicializacion.Seccion));
        services.AddScoped<InicializadorDatos>();

        return services;
    }

    private static void AgregarPersistencia(IServiceCollection services, IConfiguration configuration)
    {
        var cadenaConexion = configuration.GetConnectionString(NombreCadenaConexion);
        if (string.IsNullOrWhiteSpace(cadenaConexion))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{NombreCadenaConexion}'. Ver back/README.md.");
        }

        services.Configure<OpcionesBaseDatos>(configuration.GetSection(OpcionesBaseDatos.Seccion));

        services.AddScoped<ProteccionSedeInterceptor>();
        services.AddScoped<AuditoriaInterceptor>();

        services.AddDbContext<AppDbContext>((proveedor, opciones) => opciones
            .UseSqlServer(cadenaConexion)
            // Primero la protección de sede (si detecta una violación, no se audita ni se guarda nada); luego la auditoría.
            .AddInterceptors(
                proveedor.GetRequiredService<ProteccionSedeInterceptor>(),
                proveedor.GetRequiredService<AuditoriaInterceptor>())
            // EF Core advierte que Usuario y Rol tienen filtro de sede y son el lado obligatorio de relaciones
            // (cuentas, roles asignados, permisos). Es intencional: esas tablas solo se consultan a través de su
            // agregado o por la cuenta del propio usuario (docs/ARQUITECTURA.md §5.6).
            .ConfigureWarnings(advertencias => advertencias.Ignore(
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        services.AddScoped<IAppDbContext>(proveedor => proveedor.GetRequiredService<AppDbContext>());

        // Las claves que cifran la cookie de sesión se guardan en la base de datos (docs/ARQUITECTURA.md §6.1).
        services.AddDataProtection()
            .SetApplicationName("InventarioTIC")
            .PersistKeysToDbContext<AppDbContext>();
    }

    private static void AgregarIdentidad(IServiceCollection services)
    {
        // Valores de seguridad acordados (docs/ARQUITECTURA.md D17).
        services.AddIdentityCore<CuentaUsuario>(opciones =>
            {
                opciones.Password.RequiredLength = 10;
                opciones.Password.RequireDigit = true;
                opciones.Password.RequireLowercase = true;
                opciones.Password.RequireUppercase = true;
                opciones.Password.RequireNonAlphanumeric = false;
                opciones.Password.RequiredUniqueChars = 4;

                opciones.Lockout.AllowedForNewUsers = true;
                opciones.Lockout.MaxFailedAccessAttempts = 3;
                opciones.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                opciones.User.RequireUniqueEmail = true;
                opciones.SignIn.RequireConfirmedAccount = false;
            })
            .AddSignInManager()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<DescriptorErroresIdentidad>()
            .AddClaimsPrincipalFactory<FabricaPrincipal>();

        // El enlace de "olvidé mi contraseña" vence en 1 hora.
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

        // Cada minuto se revisa que la sesión siga siendo válida: si cambian la contraseña o la cuenta se desactiva,
        // las demás sesiones abiertas de ese usuario se cierran en menos de un minuto.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddScoped<IServicioIdentidad, ServicioIdentidad>();
    }

    private static void AgregarCorreoYEnlaces(IServiceCollection services, IConfiguration configuration)
    {
        var seccionCorreo = configuration.GetSection(OpcionesCorreo.Seccion);
        services.Configure<OpcionesCorreo>(seccionCorreo);

        var modoCarpeta = string.Equals(seccionCorreo["Modo"], "Carpeta", StringComparison.OrdinalIgnoreCase);
        if (modoCarpeta)
        {
            services.AddScoped<IServicioCorreo, CorreoEnCarpeta>();
        }
        else
        {
            services.AddScoped<IServicioCorreo, CorreoSmtp>();
        }

        services.Configure<OpcionesAplicacion>(configuration.GetSection(OpcionesAplicacion.Seccion));
        services.AddScoped<IEnlacesAplicacion, EnlacesAplicacion>();
    }
}
