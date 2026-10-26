using Inventario.Application.Common.Archivos;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos.Fotos;

/// <summary>Fotos de un activo: la principal primero y luego en el orden en que se subieron.</summary>
/// <remarks>Requiere Activos.Ver. Un activo de otra sede "no existe".</remarks>
public sealed record ListarFotosActivoQuery(Guid ActivoId) : IQuery<IReadOnlyList<FotoActivoDto>>;

internal sealed class ListarFotosActivoHandler(IAppDbContext db) : IQueryHandler<ListarFotosActivoQuery, IReadOnlyList<FotoActivoDto>>
{
    public async Task<Result<IReadOnlyList<FotoActivoDto>>> Handle(ListarFotosActivoQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Activos.AnyAsync(a => a.Id == query.ActivoId, cancellationToken))
        {
            return ErroresActivo.NoEncontrado;
        }

        var fotos = await db.FotosActivo.AsNoTracking()
            .Where(f => f.ActivoId == query.ActivoId)
            .OrderByDescending(f => f.Principal)
            .ThenBy(f => f.Orden)
            .ToListAsync(cancellationToken);

        return fotos.Select(ProyeccionFoto.ADto).ToList();
    }
}

/// <summary>Archivo de una foto (o su miniatura).</summary>
/// <remarks>Requiere Activos.Ver. El filtro de sede impide ver las fotos de otra red.</remarks>
public sealed record ObtenerArchivoFotoQuery(Guid ActivoId, Guid FotoId, bool Miniatura) : IQuery<ArchivoFotoDto>;

internal sealed class ObtenerArchivoFotoHandler(IAppDbContext db, IAlmacenArchivos almacen)
    : IQueryHandler<ObtenerArchivoFotoQuery, ArchivoFotoDto>
{
    public async Task<Result<ArchivoFotoDto>> Handle(ObtenerArchivoFotoQuery query, CancellationToken cancellationToken)
    {
        var foto = await db.FotosActivo.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == query.FotoId && f.ActivoId == query.ActivoId, cancellationToken);
        if (foto is null)
        {
            return ErroresFoto.NoEncontrada;
        }

        var clave = query.Miniatura ? foto.ClaveMiniatura : foto.Clave;
        var contenido = await almacen.AbrirAsync(clave, cancellationToken);
        if (contenido is null)
        {
            return ErroresFoto.NoEncontrada;
        }

        return new ArchivoFotoDto(contenido, FotoActivo.TipoContenido(query.Miniatura ? foto.TipoMiniatura : foto.Tipo));
    }
}
