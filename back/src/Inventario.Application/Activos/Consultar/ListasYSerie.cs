using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos.Consultar;

/// <summary>Una opción de una lista fija: el valor que se envía y el texto que se muestra.</summary>
public sealed record OpcionDto(string Valor, string Texto);

/// <summary>Velocidades que corresponden a un tipo de RAM.</summary>
public sealed record VelocidadesRamDto(string Tipo, IReadOnlyList<int> VelocidadesMhz);

/// <summary>Listas fijas de la ficha técnica (docs/ARQUITECTURA.md D41), para que la interfaz no las repita.</summary>
public sealed record ListasActivoDto(
    IReadOnlyList<OpcionDto> FichasTecnicas,
    IReadOnlyList<OpcionDto> Conservaciones,
    IReadOnlyList<OpcionDto> Criticidades,
    IReadOnlyList<OpcionDto> TiposAdquisicion,
    IReadOnlyList<OpcionDto> TiposDisco,
    IReadOnlyList<OpcionDto> UsosDisco,
    IReadOnlyList<OpcionDto> TiposRam,
    IReadOnlyList<int> CapacidadesDiscoGb,
    IReadOnlyList<int> CapacidadesRamGb,
    IReadOnlyList<VelocidadesRamDto> VelocidadesRam,
    int CapacidadMaximaDiscoGb);

/// <summary>Listas fijas de la ficha técnica.</summary>
/// <remarks>Requiere Activos.Ver.</remarks>
public sealed record ObtenerListasActivoQuery : IQuery<ListasActivoDto>;

/// <summary>Activo que ya usa un número de serie.</summary>
public sealed record ActivoConSerieDto(Guid Id, string CodigoPatrimonial, string Modelo, string? NumeroSerie);

/// <summary>
/// Activos de la sede que ya tienen ese número de serie, para avisar antes de guardar (no es un error: puede haber
/// series repetidas por un registro anterior incorrecto).
/// </summary>
/// <param name="NumeroSerie">Número de serie a buscar (sin distinguir mayúsculas).</param>
/// <param name="ExcluirId">El activo que se está editando.</param>
/// <remarks>Requiere Activos.Ver.</remarks>
public sealed record BuscarSerieActivoQuery(string? NumeroSerie, Guid? ExcluirId) : IQuery<IReadOnlyList<ActivoConSerieDto>>;

internal sealed class ObtenerListasActivoHandler : IQueryHandler<ObtenerListasActivoQuery, ListasActivoDto>
{
    private static readonly ListasActivoDto Listas = new(
        [
            new OpcionDto(nameof(FichaTecnica.Computo), "Cómputo (todo, con discos y RAM)"),
            new OpcionDto(nameof(FichaTecnica.Red), "Red (nombre de equipo, IP y MAC)"),
            new OpcionDto(nameof(FichaTecnica.Ninguna), "Ninguna"),
        ],
        Opciones<Conservacion>(),
        Opciones<Criticidad>(),
        Opciones<TipoAdquisicion>(),
        Opciones<TipoDisco>(),
        Opciones<UsoDisco>(),
        Opciones<TipoRam>(),
        ListasActivo.CapacidadesDiscoGb,
        ListasActivo.CapacidadesRamGb,
        ListasActivo.VelocidadesRamMhz.Select(v => new VelocidadesRamDto(v.Key.ToString(), v.Value)).ToList(),
        ListasActivo.CapacidadMaximaDiscoGb);

    public Task<Result<ListasActivoDto>> Handle(ObtenerListasActivoQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Exito(Listas));

    private static List<OpcionDto> Opciones<T>()
        where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new OpcionDto(v.ToString(), ListasActivo.Texto(v))).ToList();
}

internal sealed class BuscarSerieActivoHandler(IAppDbContext db)
    : IQueryHandler<BuscarSerieActivoQuery, IReadOnlyList<ActivoConSerieDto>>
{
    public async Task<Result<IReadOnlyList<ActivoConSerieDto>>> Handle(BuscarSerieActivoQuery query, CancellationToken cancellationToken)
    {
        var serie = query.NumeroSerie?.Trim();
        if (string.IsNullOrEmpty(serie))
        {
            return Result.Exito<IReadOnlyList<ActivoConSerieDto>>([]);
        }

        var excluir = query.ExcluirId ?? Guid.Empty;

        // La comparación de la base de datos no distingue mayúsculas. El filtro de sede limita a la sede del usuario.
        var activos = await db.Activos
            .AsNoTracking()
            .Where(a => a.NumeroSerie == serie && a.Id != excluir)
            .OrderBy(a => a.CodigoPatrimonial)
            .Take(10)
            .Select(a => new ActivoConSerieDto(a.Id, a.CodigoPatrimonial, a.Modelo, a.NumeroSerie))
            .ToListAsync(cancellationToken);

        return Result.Exito<IReadOnlyList<ActivoConSerieDto>>(activos);
    }
}
