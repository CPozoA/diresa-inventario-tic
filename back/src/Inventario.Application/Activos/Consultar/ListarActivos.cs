using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos.Consultar;

/// <summary>Un activo en la lista del inventario.</summary>
/// <remarks>
/// <c>Responsable</c> es la persona que lo tiene asignado (D51); si no hay asignación, el texto escrito antes del módulo
/// de asignaciones ("responsable anterior", D52). <c>ResponsableAsignado</c> distingue los dos casos.
/// </remarks>
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
    bool ResponsableAsignado,
    string Estado,
    EstadoGarantia Garantia,
    SituacionRenovacion Renovacion,
    Guid SedeId,
    string SedeNombre,
    Guid? FotoPrincipalId);

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
/// <param name="Atencion">Los que requieren atención por ese motivo, sin los de baja (como en el resumen, D49).</param>
/// <param name="Falta">Los que no tienen ese dato en su ficha, sin los de baja.</param>
/// <param name="Buscar">Código patrimonial, serie, modelo, marca, IP, nombre de equipo, nombre o DNI de quien lo tiene asignado, o el responsable anterior.</param>
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
    MotivoAtencion? Atencion,
    DatoFaltante? Falta,
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
        var pagina = Math.Max(1, query.Pagina ?? 1);
        var tamano = Math.Clamp(query.TamanoPagina ?? TamanoPorOmision, 1, TamanoMaximo);

        var filtros = new FiltrosInventario(
            query.SedeId, query.EstablecimientoId, query.TipoActivoId, query.AreaId, query.EstadoActivoId, query.Garantia,
            query.Renovacion, query.Atencion, query.Falta, query.Buscar);
        var filas = ConsultaInventario.Filas(db, filtros, hoy);

        var total = await filas.CountAsync(cancellationToken);

        var pagina1 = await filas
            .OrderBy(f => f.CodigoPatrimonial)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancellationToken);

        // Foto principal de cada activo de la página, para la miniatura (D38).
        var ids = pagina1.Select(f => f.Id).ToList();
        var fotos = await db.FotosActivo.AsNoTracking()
            .Where(f => ids.Contains(f.ActivoId) && f.Principal)
            .Select(f => new { f.ActivoId, f.Id })
            .ToListAsync(cancellationToken);
        var principales = fotos.GroupBy(f => f.ActivoId).ToDictionary(g => g.Key, g => g.First().Id);

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
                f.Persona ?? f.ResponsableAnterior,
                f.Persona != null,
                f.Estado,
                Activo.GarantiaAl(f.FinGarantia, hoy),
                Activo.RenovacionAl(f.FechaRenovacion, hoy),
                f.SedeId,
                f.SedeNombre,
                principales.TryGetValue(f.Id, out var foto) ? foto : null))
            .ToList();

        return new PaginaActivosDto(lista, total, pagina, tamano);
    }
}
