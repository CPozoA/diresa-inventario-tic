using Inventario.Domain.Catalogos;
using Inventario.Domain.Sedes;
using Inventario.Domain.Seguridad;
using Inventario.Domain.Usuarios;
using Inventario.Infrastructure.Identidad;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Inventario.Infrastructure.Persistencia.Inicializacion;

/// <summary>
/// Carga los datos con los que arranca el sistema: las sedes, los roles del sistema, los roles iniciales
/// (Editor y Consulta) de cada sede nueva y la cuenta del superadministrador. Es idempotente: solo agrega
/// lo que falta, así que se puede ejecutar en cada arranque sin duplicar nada.
/// </summary>
internal sealed partial class InicializadorDatos(
    AppDbContext db,
    UserManager<CuentaUsuario> cuentas,
    IOptions<OpcionesBaseDatos> opcionesBaseDatos,
    IOptions<OpcionesInicializacion> opcionesInicializacion,
    ILogger<InicializadorDatos> logger)
{
    public async Task EjecutarAsync(CancellationToken cancellationToken)
    {
        if (!db.Database.GetMigrations().Any())
        {
            LogSinMigraciones(logger);
            return;
        }

        if (opcionesBaseDatos.Value.AplicarMigracionesAlIniciar)
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

        await CrearSedesYRolesAsync(cancellationToken);
        await CrearCatalogosGeneralesAsync(cancellationToken);
        await CrearSuperAdminSiFaltaAsync(cancellationToken);
    }

    private async Task CrearSedesYRolesAsync(CancellationToken cancellationToken)
    {
        var sedesExistentes = await db.Sedes.Select(s => s.Id).ToListAsync(cancellationToken);
        var sedesNuevas = SedesIniciales.Crear().Where(s => !sedesExistentes.Contains(s.Id)).ToList();
        db.Sedes.AddRange(sedesNuevas);

        var rolesExistentes = await db.Roles.Where(r => r.SedeId == null).Select(r => r.Id).ToListAsync(cancellationToken);
        var rolesSistemaNuevos = RolesDelSistema.Crear().Where(r => !rolesExistentes.Contains(r.Id)).ToList();
        db.Roles.AddRange(rolesSistemaNuevos);

        foreach (var sede in sedesNuevas)
        {
            db.Roles.AddRange(RolesDelSistema.CrearInicialesDeSede(sede.Id));
        }

        if (sedesNuevas.Count > 0 || rolesSistemaNuevos.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            LogDatosCreados(logger, sedesNuevas.Count, rolesSistemaNuevos.Count);
        }
    }

    /// <summary>
    /// Carga los valores iniciales de cada catálogo general que esté vacío. Si el superadministrador ya lo modificó
    /// (aunque sea desactivando todo), no se toca.
    /// </summary>
    private async Task CrearCatalogosGeneralesAsync(CancellationToken cancellationToken)
    {
        var cargados = 0;
        cargados += await CargarSiVacioAsync(db.TiposActivo, CatalogosIniciales.TiposActivo, TipoActivo.Crear, cancellationToken);
        cargados += await CargarSiVacioAsync(db.Marcas, CatalogosIniciales.Marcas, Marca.Crear, cancellationToken);
        cargados += await CargarSiVacioAsync(db.EstadosActivo, CatalogosIniciales.EstadosActivo, EstadoActivo.Crear, cancellationToken);
        cargados += await CargarSiVacioAsync(db.SistemasOperativos, CatalogosIniciales.SistemasOperativos, SistemaOperativo.Crear, cancellationToken);
        cargados += await CargarSiVacioAsync(db.TiposLicencia, CatalogosIniciales.TiposLicencia, TipoLicencia.Crear, cancellationToken);
        cargados += await CargarSiVacioAsync(db.Procesadores, CatalogosIniciales.Procesadores, Procesador.Crear, cancellationToken);
        cargados += await CargarSiVacioAsync(db.Antivirus, CatalogosIniciales.Antivirus, Antivirus.Crear, cancellationToken);

        if (cargados > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            LogCatalogosCargados(logger, cargados);
        }

        await CompletarFichasTecnicasAsync(cancellationToken);
        await CompletarGruposDeEstadoAsync(cancellationToken);
    }

    /// <summary>
    /// Los estados registrados antes del Paso 8 no tienen grupo (D48): se les asigna el que corresponde por su nombre.
    /// Solo ocurre una vez; después lo cambia el superadministrador.
    /// </summary>
    private async Task CompletarGruposDeEstadoAsync(CancellationToken cancellationToken)
    {
        var pendientes = await db.EstadosActivo.Where(e => e.GrupoAsignado == null).ToListAsync(cancellationToken);
        var completados = pendientes.Count(e => e.CompletarGrupoSiFalta());

        if (completados > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            LogGruposCompletados(logger, completados);
        }
    }

    /// <summary>
    /// Los tipos de activo registrados antes del Paso 7C no tienen ficha técnica (D43): se les asigna la que
    /// corresponde por su nombre. Solo ocurre una vez; después la cambia el superadministrador.
    /// </summary>
    private async Task CompletarFichasTecnicasAsync(CancellationToken cancellationToken)
    {
        var pendientes = await db.TiposActivo.Where(t => t.FichaAsignada == null).ToListAsync(cancellationToken);
        var completados = pendientes.Count(t => t.CompletarFichaSiFalta());

        if (completados > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            LogFichasCompletadas(logger, completados);
        }
    }

    private static async Task<int> CargarSiVacioAsync<T>(
        DbSet<T> tabla,
        IReadOnlyList<string> nombres,
        Func<string, T> crear,
        CancellationToken cancellationToken)
        where T : ElementoCatalogo
    {
        if (await tabla.AnyAsync(cancellationToken))
        {
            return 0;
        }

        tabla.AddRange(nombres.Select(crear));
        return 1;
    }

    private async Task CrearSuperAdminSiFaltaAsync(CancellationToken cancellationToken)
    {
        if (await db.Usuarios.AnyAsync(u => u.SedeId == null, cancellationToken))
        {
            return;
        }

        var datos = opcionesInicializacion.Value.SuperAdmin;
        if (!datos.EstaCompleto)
        {
            LogSuperAdminSinConfigurar(logger);
            return;
        }

        var usuario = Usuario.CrearSuperAdmin(datos.Nombres, datos.Apellidos);
        db.Usuarios.Add(usuario);

        var cuenta = new CuentaUsuario
        {
            Id = usuario.Id,
            UserName = datos.NombreUsuario.Trim(),
            Email = datos.Correo.Trim(),
            EmailConfirmed = true,
            DebeCambiarContrasena = true,
        };

        // CreateAsync valida la contraseña con la política (mínimo 10, mayúscula, minúscula y número)
        // y guarda en una sola operación el usuario, su rol y su cuenta.
        var resultado = await cuentas.CreateAsync(cuenta, datos.ContrasenaInicial);
        if (!resultado.Succeeded)
        {
            db.Entry(usuario).State = EntityState.Detached;
            var errores = string.Join(" ", resultado.Errors.Select(e => e.Description));
            LogSuperAdminNoCreado(logger, errores);
            return;
        }

        LogSuperAdminCreado(logger, cuenta.UserName);
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

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning,
        Message = "No existe el superadministrador y faltan sus datos en 'Inicializacion:SuperAdmin' (usuario, correo y contraseña inicial). Ver back/README.md.")]
    private static partial void LogSuperAdminSinConfigurar(ILogger logger);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "No se pudo crear el superadministrador: {Errores}")]
    private static partial void LogSuperAdminNoCreado(ILogger logger, string errores);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information,
        Message = "Superadministrador '{NombreUsuario}' creado. Deberá cambiar la contraseña temporal al iniciar sesión.")]
    private static partial void LogSuperAdminCreado(ILogger logger, string nombreUsuario);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Information, Message = "Catálogos generales cargados con sus valores iniciales: {Cantidad}.")]
    private static partial void LogCatalogosCargados(ILogger logger, int cantidad);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Information, Message = "Ficha técnica asignada por su nombre a {Cantidad} tipos de activo.")]
    private static partial void LogFichasCompletadas(ILogger logger, int cantidad);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Information, Message = "Grupo asignado por su nombre a {Cantidad} estados del activo.")]
    private static partial void LogGruposCompletados(ILogger logger, int cantidad);
}
