using Inventario.Application.Activos;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Asignaciones;

/// <summary>Historial de asignaciones del activo: la vigente primero y luego las más recientes.</summary>
/// <remarks>Requiere Activos.Ver. Un activo de otra sede "no existe".</remarks>
public sealed record ListarAsignacionesActivoQuery(Guid ActivoId) : IQuery<IReadOnlyList<AsignacionActivoDto>>;

/// <summary>Entregas y devoluciones más recientes de la red (pestaña "Entregas y devoluciones").</summary>
/// <param name="SedeId">Solo para el superadministrador: limita a una red.</param>
/// <param name="Maximo">Entre 1 y 200 (por omisión, 50).</param>
/// <remarks>Requiere Activos.Ver.</remarks>
public sealed record ListarMovimientosQuery(Guid? SedeId, int? Maximo) : IQuery<IReadOnlyList<MovimientoDto>>;

internal sealed class ListarAsignacionesActivoHandler(IAppDbContext db)
    : IQueryHandler<ListarAsignacionesActivoQuery, IReadOnlyList<AsignacionActivoDto>>
{
    public async Task<Result<IReadOnlyList<AsignacionActivoDto>>> Handle(ListarAsignacionesActivoQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Activos.AnyAsync(a => a.Id == query.ActivoId, cancellationToken))
        {
            return ErroresActivo.NoEncontrado;
        }

        return await ConsultasAsignacion.DelActivo(db, query.ActivoId).ToListAsync(cancellationToken);
    }
}

internal sealed class ListarMovimientosHandler(IAppDbContext db) : IQueryHandler<ListarMovimientosQuery, IReadOnlyList<MovimientoDto>>
{
    public const int MaximoPorOmision = 50;
    public const int MaximoPermitido = 200;

    public async Task<Result<IReadOnlyList<MovimientoDto>>> Handle(ListarMovimientosQuery query, CancellationToken cancellationToken)
    {
        var maximo = Math.Clamp(query.Maximo ?? MaximoPorOmision, 1, MaximoPermitido);

        // El filtro global de sede ya limita a un usuario de sede a la suya.
        var asignaciones = db.Asignaciones.AsNoTracking();
        if (query.SedeId is Guid sedeId)
        {
            asignaciones = asignaciones.Where(x => x.SedeId == sedeId);
        }

        var filas =
            from x in asignaciones
            join a in db.Activos on x.ActivoId equals a.Id
            join t in db.TiposActivo on a.TipoActivoId equals t.Id
            join m in db.Marcas on a.MarcaId equals m.Id
            join p in db.Personal on x.PersonaId equals p.Id
            join s in db.Sedes on x.SedeId equals s.Id
            select new
            {
                x.Id,
                x.FechaEntrega,
                x.ConservacionEntrega,
                x.FechaDevolucion,
                x.ConservacionDevolucion,
                x.CreadoEn,
                x.ModificadoEn,
                ActivoId = a.Id,
                a.CodigoPatrimonial,
                TipoActivo = t.Nombre,
                Marca = m.Nombre,
                a.Modelo,
                PersonaId = p.Id,
                p.NombreCompleto,
                SedeNombre = s.Nombre,
            };

        // Las entregas y las devoluciones más recientes; luego se mezclan y se cortan al máximo.
        var entregas = await filas
            .OrderByDescending(f => f.FechaEntrega)
            .ThenByDescending(f => f.CreadoEn)
            .Take(maximo)
            .ToListAsync(cancellationToken);
        var devoluciones = await filas
            .Where(f => f.FechaDevolucion != null)
            .OrderByDescending(f => f.FechaDevolucion)
            .ThenByDescending(f => f.ModificadoEn)
            .Take(maximo)
            .ToListAsync(cancellationToken);

        var movimientos = entregas
            .Select(f => new
            {
                Dto = new MovimientoDto(
                    f.Id, TipoMovimiento.Entrega, f.FechaEntrega, f.ActivoId, f.CodigoPatrimonial, f.TipoActivo, f.Marca, f.Modelo,
                    f.PersonaId, f.NombreCompleto, f.ConservacionEntrega, f.SedeNombre),
                Registrado = f.CreadoEn,
            })
            .Concat(devoluciones.Select(f => new
            {
                Dto = new MovimientoDto(
                    f.Id, TipoMovimiento.Devolucion, f.FechaDevolucion!.Value, f.ActivoId, f.CodigoPatrimonial, f.TipoActivo, f.Marca, f.Modelo,
                    f.PersonaId, f.NombreCompleto, f.ConservacionDevolucion!.Value, f.SedeNombre),
                Registrado = f.ModificadoEn ?? f.CreadoEn,
            }))
            // Lo más reciente arriba; en una reasignación (mismo día y momento) la nueva entrega va antes que la devolución.
            .OrderByDescending(m => m.Dto.Fecha)
            .ThenByDescending(m => m.Registrado)
            .ThenBy(m => m.Dto.Tipo)
            .Take(maximo)
            .Select(m => m.Dto)
            .ToList();

        return movimientos;
    }
}
