using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos.Consultar;

/// <summary>Un activo en la lista del inventario.</summary>
public sealed record ActivoResumenDto(
    Guid Id,
    string CodigoPatrimonial,
    string TipoActivo,
    string Marca,
    string Modelo,
    string? NumeroSerie,
    string Establecimiento,
    string Area,
    string? Responsable,
    string Estado,
    EstadoGarantia Garantia,
    SituacionRenovacion Renovacion,
    Guid SedeId,
    string SedeNombre);

/// <summary>Una página de la lista y el total de activos que cumplen los filtros.</summary>
public sealed record PaginaActivosDto(IReadOnlyList<ActivoResumenDto> Activos, int Total, int Pagina, int TamanoPagina);

/// <summary>
/// Inventario paginado y ordenado por código patrimonial. Un usuario de sede solo ve su sede; el superadministrador
/// ve todas o filtra por una.
/// </summary>
/// <param name="SedeId">Solo para el superadministrador.</param>
/// <param name="EstablecimientoId">Filtra por establecimiento.</param>
/// <param name="TipoActivoId">Filtra por tipo.</param>
/// <param name="AreaId">Filtra por área.</param>
/// <param name="EstadoActivoId">Filtra por estado.</param>
/// <param name="Garantia">Filtra por estado de la garantía hoy.</param>
/// <param name="Renovacion">Filtra por situación de renovación hoy.</param>
/// <param name="Buscar">Código patrimonial, serie, modelo, responsable o marca.</param>
/// <param name="Pagina">Desde 1.</param>
/// <param name="TamanoPagina">Entre 1 y 100 (por omisión, 25).</param>
/// <remarks>Requiere Activos.Ver.</remarks>
public sealed record ListarActivosQuery(
    Guid? SedeId,
    Guid? EstablecimientoId,
    Guid? TipoActivoId,
    Guid? AreaId,
    Guid? EstadoActivoId,
    EstadoGarantia? Garantia,
    SituacionRenovacion? Renovacion,
    string? Buscar,
    int? Pagina,
    int? TamanoPagina) : IQuery<PaginaActivosDto>;

internal sealed class ListarActivosHandler(IAppDbContext db, TimeProvider reloj) : IQueryHandler<ListarActivosQuery, PaginaActivosDto>
{
    public const int TamanoPorOmision = 25;
    public const int TamanoMaximo = 100;

    public async Task<Result<PaginaActivosDto>> Handle(ListarActivosQuery query, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var enUnAnio = hoy.AddYears(ListasActivo.AniosAvisoRenovacion);
        var pagina = Math.Max(1, query.Pagina ?? 1);
        var tamano = Math.Clamp(query.TamanoPagina ?? TamanoPorOmision, 1, TamanoMaximo);

        // El filtro global de sede ya limita a un usuario de sede a la suya.
        var activos = db.Activos.AsNoTracking();

        if (query.SedeId is Guid sedeId)
        {
            activos = activos.Where(a => a.SedeId == sedeId);
        }

        if (query.EstablecimientoId is Guid establecimientoId)
        {
            activos = activos.Where(a => a.EstablecimientoId == establecimientoId);
        }

        if (query.TipoActivoId is Guid tipoId)
        {
            activos = activos.Where(a => a.TipoActivoId == tipoId);
        }

        if (query.AreaId is Guid areaId)
        {
            activos = activos.Where(a => a.AreaId == areaId);
        }

        if (query.EstadoActivoId is Guid estadoId)
        {
            activos = activos.Where(a => a.EstadoActivoId == estadoId);
        }

        activos = query.Garantia switch
        {
            EstadoGarantia.SinDato => activos.Where(a => a.FinGarantia == null),
            EstadoGarantia.Vigente => activos.Where(a => a.FinGarantia >= hoy),
            EstadoGarantia.Vencida => activos.Where(a => a.FinGarantia < hoy),
            _ => activos,
        };

        activos = query.Renovacion switch
        {
            SituacionRenovacion.SinDato => activos.Where(a => a.FechaRenovacion == null),
            SituacionRenovacion.Renovar => activos.Where(a => a.FechaRenovacion <= hoy),
            SituacionRenovacion.ProximoARenovar => activos.Where(a => a.FechaRenovacion > hoy && a.FechaRenovacion <= enUnAnio),
            SituacionRenovacion.Vigente => activos.Where(a => a.FechaRenovacion > enUnAnio),
            _ => activos,
        };

        var filas =
            from a in activos
            join t in db.TiposActivo on a.TipoActivoId equals t.Id
            join m in db.Marcas on a.MarcaId equals m.Id
            join ar in db.Areas on a.AreaId equals ar.Id
            join es in db.EstadosActivo on a.EstadoActivoId equals es.Id
            join e in db.Establecimientos on a.EstablecimientoId equals e.Id
            join s in db.Sedes on a.SedeId equals s.Id
            select new
            {
                a.Id,
                a.CodigoPatrimonial,
                TipoActivo = t.Nombre,
                Marca = m.Nombre,
                a.Modelo,
                a.NumeroSerie,
                Establecimiento = e.Nombre,
                Area = ar.Nombre,
                a.Responsable,
                Estado = es.Nombre,
                a.FinGarantia,
                a.FechaRenovacion,
                a.SedeId,
                SedeNombre = s.Nombre,
            };

        var buscar = query.Buscar?.Trim();
        if (!string.IsNullOrEmpty(buscar))
        {
            filas = filas.Where(f =>
                f.CodigoPatrimonial.Contains(buscar)
                || (f.NumeroSerie != null && f.NumeroSerie.Contains(buscar))
                || f.Modelo.Contains(buscar)
                || (f.Responsable != null && f.Responsable.Contains(buscar))
                || f.Marca.Contains(buscar));
        }

        var total = await filas.CountAsync(cancellationToken);

        var pagina1 = await filas
            .OrderBy(f => f.CodigoPatrimonial)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancellationToken);

        var lista = pagina1
            .Select(f => new ActivoResumenDto(
                f.Id,
                f.CodigoPatrimonial,
                f.TipoActivo,
                f.Marca,
                f.Modelo,
                f.NumeroSerie,
                f.Establecimiento,
                f.Area,
                f.Responsable,
                f.Estado,
                Activo.GarantiaAl(f.FinGarantia, hoy),
                Activo.RenovacionAl(f.FechaRenovacion, hoy),
                f.SedeId,
                f.SedeNombre))
            .ToList();

        return new PaginaActivosDto(lista, total, pagina, tamano);
    }
}
