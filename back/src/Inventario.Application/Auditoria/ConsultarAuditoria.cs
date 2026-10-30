using Inventario.Application.Activos;
using Inventario.Application.Activos.Exportar;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Auditoria;

/// <summary>Filtros del registro de auditoría.</summary>
/// <param name="SedeId">Solo para el superadministrador (un administrador de sede ve siempre la suya).</param>
/// <param name="Desde">Desde este día (hora local), incluido.</param>
/// <param name="Hasta">Hasta este día (hora local), incluido.</param>
/// <param name="Modulo">Módulo.</param>
/// <param name="Buscar">En la acción ("PAT-0001", "licencia"), el nombre de quien la hizo o la IP.</param>
public sealed record FiltrosAuditoria(Guid? SedeId, DateOnly? Desde, DateOnly? Hasta, ModuloAuditoria? Modulo, string? Buscar);

/// <summary>Una acción registrada.</summary>
/// <param name="Id">Registro.</param>
/// <param name="Fecha">Cuándo (UTC; la interfaz la muestra en la hora local).</param>
/// <param name="UsuarioId">Quién; nulo en un inicio de sesión fallido con un usuario que no existe.</param>
/// <param name="Usuario">Nombre de quien la hizo.</param>
/// <param name="Modulo">Módulo.</param>
/// <param name="Accion">Qué hizo, en palabras.</param>
/// <param name="SedeNombre">Red del registro afectado; nula si no es de una red.</param>
/// <param name="ActivoId">Activo afectado, para abrir su ficha.</param>
/// <param name="Ip">Dirección IP de origen.</param>
public sealed record RegistroAuditoriaDto(
    Guid Id,
    DateTimeOffset Fecha,
    Guid? UsuarioId,
    string? Usuario,
    ModuloAuditoria Modulo,
    string Accion,
    string? SedeNombre,
    Guid? ActivoId,
    string? Ip);

/// <summary>Una página del registro y el total que cumple los filtros.</summary>
public sealed record PaginaAuditoriaDto(IReadOnlyList<RegistroAuditoriaDto> Registros, int Total, int Pagina, int TamanoPagina);

/// <summary>Registro de auditoría, lo más reciente primero (D56).</summary>
/// <param name="Filtros">Filtros.</param>
/// <param name="Pagina">Desde 1.</param>
/// <param name="TamanoPagina">Entre 1 y 100 (por omisión, 50).</param>
/// <remarks>Requiere Auditoria.Ver. Un administrador de sede ve solo lo de su red; el superadministrador, todo.</remarks>
public sealed record ListarAuditoriaQuery(FiltrosAuditoria Filtros, int? Pagina, int? TamanoPagina) : IQuery<PaginaAuditoriaDto>;

/// <summary>Las acciones sobre un activo (su "Historial"), lo más reciente primero.</summary>
/// <remarks>Requiere Auditoria.Ver. Un activo de otra sede "no existe".</remarks>
public sealed record ObtenerHistorialActivoQuery(Guid ActivoId) : IQuery<IReadOnlyList<RegistroAuditoriaDto>>;

/// <summary>El registro de auditoría en Excel, con los mismos filtros de la página.</summary>
/// <remarks>Requiere Auditoria.Ver.</remarks>
public sealed record ExportarAuditoriaQuery(FiltrosAuditoria Filtros) : IQuery<ArchivoDescargable>;

/// <summary>Datos del registro de auditoría para el Excel.</summary>
/// <param name="Ambito">"Red de Salud Huanta" o "Todas las redes".</param>
/// <param name="GeneradoEn">Fecha y hora de la exportación (local).</param>
/// <param name="Filtros">Filtros en palabras.</param>
/// <param name="ConRed">Incluye la columna "Red".</param>
/// <param name="Registros">Una fila por acción, lo más reciente primero.</param>
public sealed record AuditoriaParaExcel(
    string Ambito, DateTime GeneradoEn, IReadOnlyList<string> Filtros, bool ConRed, IReadOnlyList<AccionParaExcel> Registros);

/// <summary>Una acción como se escribe en el Excel.</summary>
public sealed record AccionParaExcel(DateTime Fecha, string Usuario, string Red, string Modulo, string Accion, string? Ip);

/// <summary>Arma el Excel del registro de auditoría (ClosedXML).</summary>
public interface IGeneradorExcelAuditoria
{
    byte[] Generar(AuditoriaParaExcel datos);
}

/// <summary>Errores del registro de auditoría.</summary>
public static class ErroresAuditoria
{
    public static readonly ErrorAplicacion DemasiadosRegistros =
        ErrorAplicacion.ReglaDeNegocio(
            "Auditoria.DemasiadosRegistros",
            "Son más de 50 000 acciones; acote las fechas o el módulo para exportarlas.");

    public static readonly ErrorAplicacion FechasInvalidas =
        ErrorAplicacion.Validacion("Auditoria.FechasInvalidas", "La fecha «desde» no puede ser posterior a «hasta».");
}

/// <summary>Una acción con los nombres ya resueltos, como fila de consulta (propiedades para que EF Core filtre y ordene).</summary>
internal sealed class FilaAuditoria
{
    public Guid Id { get; init; }

    public DateTimeOffset Fecha { get; init; }

    public Guid? UsuarioId { get; init; }

    public string? Usuario { get; init; }

    public ModuloAuditoria Modulo { get; init; }

    public string Accion { get; init; } = string.Empty;

    public Guid? SedeId { get; init; }

    public string? SedeNombre { get; init; }

    public Guid? ActivoId { get; init; }

    public string? Ip { get; init; }

    public RegistroAuditoriaDto ADto() => new(Id, Fecha, UsuarioId, Usuario, Modulo, Accion, SedeNombre, ActivoId, Ip);
}

internal static class ConsultaAuditoria
{
    /// <summary>
    /// Acciones que puede ver quien consulta: un usuario de sede, las de su red; el superadministrador, todas (o las de la
    /// red que indique).
    /// </summary>
    public static IQueryable<FilaAuditoria> Filas(IAppDbContext db, IUsuarioActual usuario, FiltrosAuditoria filtros, TimeZoneInfo zona)
    {
        var registros = db.Auditoria.AsNoTracking();

        if (!usuario.EsSuperAdmin)
        {
            var propia = usuario.SedeId;
            registros = registros.Where(r => propia != null && r.SedeId == propia);
        }
        else if (filtros.SedeId is Guid sedeId)
        {
            registros = registros.Where(r => r.SedeId == sedeId);
        }

        if (filtros.Desde is DateOnly desde)
        {
            var inicio = InicioDelDia(desde, zona);
            registros = registros.Where(r => r.Fecha >= inicio);
        }

        if (filtros.Hasta is DateOnly hasta)
        {
            var fin = InicioDelDia(hasta.AddDays(1), zona);
            registros = registros.Where(r => r.Fecha < fin);
        }

        if (filtros.Modulo is ModuloAuditoria modulo)
        {
            registros = registros.Where(r => r.Modulo == modulo);
        }

        // Excepción revisada al filtro de sede: de los usuarios solo se lee el nombre de quien hizo la acción (por
        // ejemplo, el superadministrador en el registro de una red).
        var filas =
            from r in registros
            join u in db.Usuarios.IgnoreQueryFilters() on r.UsuarioId equals (Guid?)u.Id into usuarios
            from u in usuarios.DefaultIfEmpty()
            join s in db.Sedes.IgnoreQueryFilters() on r.SedeId equals (Guid?)s.Id into sedes
            from s in sedes.DefaultIfEmpty()
            select new FilaAuditoria
            {
                Id = r.Id,
                Fecha = r.Fecha,
                UsuarioId = r.UsuarioId,
                Usuario = u != null ? u.Nombres + " " + u.Apellidos : null,
                Modulo = r.Modulo,
                Accion = r.Accion,
                SedeId = r.SedeId,
                SedeNombre = s != null ? s.Nombre : null,
                ActivoId = r.ActivoId,
                Ip = r.Ip,
            };

        var buscar = filtros.Buscar?.Trim();
        if (!string.IsNullOrEmpty(buscar))
        {
            filas = filas.Where(f =>
                f.Accion.Contains(buscar)
                || (f.Usuario != null && f.Usuario.Contains(buscar))
                || (f.Ip != null && f.Ip.StartsWith(buscar)));
        }

        return filas;
    }

    public static string TextoModulo(ModuloAuditoria modulo) => modulo switch
    {
        ModuloAuditoria.Catalogos => "Catálogos",
        ModuloAuditoria.Cuenta => "Cuenta y sesiones",
        _ => modulo.ToString(),
    };

    private static DateTimeOffset InicioDelDia(DateOnly dia, TimeZoneInfo zona)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zona.GetUtcOffset(local));
    }
}

internal sealed class ListarAuditoriaHandler(IAppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
    : IQueryHandler<ListarAuditoriaQuery, PaginaAuditoriaDto>
{
    public const int TamanoPorOmision = 50;
    public const int TamanoMaximo = 100;

    public async Task<Result<PaginaAuditoriaDto>> Handle(ListarAuditoriaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query.Filtros);
        if (query.Filtros is { Desde: DateOnly desde, Hasta: DateOnly hasta } && desde > hasta)
        {
            return ErroresAuditoria.FechasInvalidas;
        }

        var pagina = Math.Max(1, query.Pagina ?? 1);
        var tamano = Math.Clamp(query.TamanoPagina ?? TamanoPorOmision, 1, TamanoMaximo);

        var filas = ConsultaAuditoria.Filas(db, usuario, query.Filtros, reloj.LocalTimeZone);
        var total = await filas.CountAsync(cancellationToken);
        var lista = await filas
            .OrderByDescending(f => f.Fecha)
            .ThenByDescending(f => f.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancellationToken);

        return new PaginaAuditoriaDto(lista.Select(f => f.ADto()).ToList(), total, pagina, tamano);
    }
}

internal sealed class ObtenerHistorialActivoHandler(IAppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
    : IQueryHandler<ObtenerHistorialActivoQuery, IReadOnlyList<RegistroAuditoriaDto>>
{
    public const int Maximo = 200;

    public async Task<Result<IReadOnlyList<RegistroAuditoriaDto>>> Handle(ObtenerHistorialActivoQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Activos.AnyAsync(a => a.Id == query.ActivoId, cancellationToken))
        {
            return ErroresActivo.NoEncontrado;
        }

        var filtros = new FiltrosAuditoria(null, null, null, null, null);
        var lista = await ConsultaAuditoria.Filas(db, usuario, filtros, reloj.LocalTimeZone)
            .Where(f => f.ActivoId == query.ActivoId)
            .OrderByDescending(f => f.Fecha)
            .ThenByDescending(f => f.Id)
            .Take(Maximo)
            .ToListAsync(cancellationToken);

        return lista.Select(f => f.ADto()).ToList();
    }
}

internal sealed class ExportarAuditoriaHandler(IAppDbContext db, IUsuarioActual usuario, TimeProvider reloj, IGeneradorExcelAuditoria generador)
    : IQueryHandler<ExportarAuditoriaQuery, ArchivoDescargable>
{
    public const int Maximo = 50_000;

    public async Task<Result<ArchivoDescargable>> Handle(ExportarAuditoriaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query.Filtros);
        var f = query.Filtros;
        if (f is { Desde: DateOnly desde, Hasta: DateOnly hasta } && desde > hasta)
        {
            return ErroresAuditoria.FechasInvalidas;
        }

        var zona = reloj.LocalTimeZone;
        var filas = ConsultaAuditoria.Filas(db, usuario, f, zona);
        if (await filas.CountAsync(cancellationToken) > Maximo)
        {
            return ErroresAuditoria.DemasiadosRegistros;
        }

        var lista = await filas.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id).ToListAsync(cancellationToken);

        var conRed = usuario.EsSuperAdmin && f.SedeId is null;
        var sedeId = f.SedeId ?? usuario.SedeId;
        var ambito = conRed
            ? "Todas las redes"
            : await db.Sedes.AsNoTracking().Where(s => s.Id == sedeId).Select(s => s.Nombre).FirstOrDefaultAsync(cancellationToken) ?? "Red";

        var descripcion = new List<string>();
        if (f.Desde is DateOnly d)
        {
            descripcion.Add("Desde: " + d.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (f.Hasta is DateOnly h)
        {
            descripcion.Add("Hasta: " + h.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (f.Modulo is ModuloAuditoria m)
        {
            descripcion.Add("Módulo: " + ConsultaAuditoria.TextoModulo(m));
        }

        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            descripcion.Add($"Búsqueda: «{f.Buscar.Trim()}»");
        }

        var ahora = reloj.GetLocalNow().DateTime;
        var datos = new AuditoriaParaExcel(
            ambito,
            ahora,
            descripcion,
            conRed,
            lista.Select(r => new AccionParaExcel(
                TimeZoneInfo.ConvertTime(r.Fecha, zona).DateTime,
                r.Usuario ?? "(usuario desconocido)",
                r.SedeNombre ?? "(general)",
                ConsultaAuditoria.TextoModulo(r.Modulo),
                r.Accion,
                r.Ip)).ToList());

        var nombre = $"Auditoria-{ExportarInventarioHandler.Slug(ambito)}-{DateOnly.FromDateTime(ahora).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.xlsx";
        return new ArchivoDescargable(generador.Generar(datos), nombre);
    }
}
