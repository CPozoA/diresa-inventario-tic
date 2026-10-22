using Inventario.Domain.Sedes;
using Inventario.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventario.Infrastructure.Persistencia.Inicializacion;

/// <summary>
/// Carga los datos con los que arranca el sistema: las sedes, los roles del sistema y los roles iniciales
/// (Editor y Consulta) de cada sede nueva. Es idempotente: solo agrega lo que falta, así que se puede
/// ejecutar en cada arranque sin duplicar nada.
/// </summary>
internal sealed partial class InicializadorDatos(
    AppDbContext db,
    IOptions<OpcionesBaseDatos> opciones,
    ILogger<InicializadorDatos> logger)
{
    public async Task EjecutarAsync(CancellationToken cancellationToken)
    {
        if (!db.Database.GetMigrations().Any())
        {
            LogSinMigraciones(logger);
            return;
        }

        if (opciones.Value.AplicarMigracionesAlIniciar)
        {
            LogAplicandoMigraciones(logger);
            await db.Database.MigrateAsync(cancellationToken);
        }

        var pendientes = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
        if (pendientes > 0)
        {
            LogMigracionesPendientes(logger, pendientes);
            return;
        }

        var sedesNuevas = await AgregarSedesFaltantesAsync(cancellationToken);
        var rolesSistemaNuevos = await AgregarRolesDelSistemaFaltantesAsync(cancellationToken);

        foreach (var sede in sedesNuevas)
        {
            db.Roles.AddRange(RolesDelSistema.CrearInicialesDeSede(sede.Id));
        }

        if (sedesNuevas.Count > 0 || rolesSistemaNuevos > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            LogDatosCreados(logger, sedesNuevas.Count, rolesSistemaNuevos);
        }
    }

    private async Task<List<Sede>> AgregarSedesFaltantesAsync(CancellationToken cancellationToken)
    {
        var existentes = await db.Sedes.Select(s => s.Id).ToListAsync(cancellationToken);
        var nuevas = SedesIniciales.Crear().Where(s => !existentes.Contains(s.Id)).ToList();

        db.Sedes.AddRange(nuevas);
        return nuevas;
    }

    private async Task<int> AgregarRolesDelSistemaFaltantesAsync(CancellationToken cancellationToken)
    {
        var existentes = await db.Roles.Where(r => r.SedeId == null).Select(r => r.Id).ToListAsync(cancellationToken);
        var nuevos = RolesDelSistema.Crear().Where(r => !existentes.Contains(r.Id)).ToList();

        db.Roles.AddRange(nuevos);
        return nuevos.Count;
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Warning,
        Message = "No hay migraciones. Cree la migración inicial con 'dotnet ef migrations add Inicial' (ver back/README.md).")]
    private static partial void LogSinMigraciones(ILogger logger);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Aplicando migraciones pendientes de la base de datos.")]
    private static partial void LogAplicandoMigraciones(ILogger logger);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning,
        Message = "Hay {Cantidad} migraciones sin aplicar. Ejecute 'dotnet ef database update' (ver back/README.md). No se cargaron datos iniciales.")]
    private static partial void LogMigracionesPendientes(ILogger logger, int cantidad);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Datos iniciales cargados: {Sedes} sedes nuevas y {Roles} roles del sistema nuevos.")]
    private static partial void LogDatosCreados(ILogger logger, int sedes, int roles);
}
