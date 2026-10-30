using Inventario.Application.Activos;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Licencias;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Licencias;

/// <summary>Licencias de la red, la que vence antes primero. Sin filtro no incluye las archivadas.</summary>
/// <param name="SedeId">Solo para el superadministrador.</param>
/// <param name="Buscar">Producto, proveedor, documento de compra o código patrimonial de un equipo.</param>
/// <param name="Filtro">Vigentes, por vencer, vencidas, excedidas o archivadas.</param>
/// <remarks>Requiere Licencias.Ver.</remarks>
public sealed record ListarLicenciasQuery(Guid? SedeId, string? Buscar, FiltroLicencias? Filtro) : IQuery<IReadOnlyList<LicenciaDto>>;

/// <summary>Cifras de licencias (página y resumen): vigentes, por vencer, vencidas, excedidas y puestos libres.</summary>
/// <remarks>Requiere Licencias.Ver.</remarks>
public sealed record ObtenerCifrasLicenciasQuery(Guid? SedeId) : IQuery<CifrasLicenciasDto>;

/// <summary>Ficha de una licencia con sus equipos.</summary>
/// <remarks>Requiere Licencias.Ver. Una licencia de otra sede "no existe".</remarks>
public sealed record ObtenerLicenciaQuery(Guid Id) : IQuery<FichaLicenciaDto>;

/// <summary>Licencias (no archivadas) que tiene un activo, la que vence antes primero.</summary>
/// <remarks>Requiere Licencias.Ver.</remarks>
public sealed record ObtenerLicenciasActivoQuery(Guid ActivoId) : IQuery<IReadOnlyList<LicenciaDelActivoDto>>;

/// <summary>Productos ya registrados, para sugerirlos al escribir.</summary>
/// <remarks>Requiere Licencias.Gestionar.</remarks>
public sealed record ListarProductosLicenciaQuery : IQuery<IReadOnlyList<string>>;

internal sealed class ListarLicenciasHandler(IAppDbContext db, TimeProvider reloj) : IQueryHandler<ListarLicenciasQuery, IReadOnlyList<LicenciaDto>>
{
    public const int MaximoResultados = 500;

    public async Task<Result<IReadOnlyList<LicenciaDto>>> Handle(ListarLicenciasQuery query, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var aviso = hoy.AddDays(ReglasVigencia.DiasAviso);

        // El filtro global de sede ya limita a un usuario de sede a la suya.
        var licencias = db.Licencias.AsNoTracking();
        if (query.SedeId is Guid sedeId)
        {
            licencias = licencias.Where(l => l.SedeId == sedeId);
        }

        licencias = query.Filtro switch
        {
            FiltroLicencias.Vigentes => licencias.Where(l => !l.Archivada && l.FechaFin >= hoy),
            FiltroLicencias.PorVencer => licencias.Where(l => !l.Archivada && l.FechaFin >= hoy && l.FechaFin <= aviso),
            FiltroLicencias.Vencidas => licencias.Where(l => !l.Archivada && l.FechaFin < hoy),
            FiltroLicencias.Excedidas => licencias.Where(l => !l.Archivada && l.Equipos.Count > l.Puestos),
            FiltroLicencias.Archivadas => licencias.Where(l => l.Archivada),
            _ => licencias.Where(l => !l.Archivada),
        };

        var buscar = query.Buscar?.Trim();
        if (!string.IsNullOrEmpty(buscar))
        {
            licencias = licencias.Where(l =>
                l.Producto.Contains(buscar)
                || (l.Proveedor != null && l.Proveedor.Contains(buscar))
                || (l.DocumentoCompra != null && l.DocumentoCompra.Contains(buscar))
                || l.Equipos.Any(e => db.Activos.Any(a => a.Id == e.ActivoId && a.CodigoPatrimonial.Contains(buscar))));
        }

        var filas = await (
            from l in licencias
            join s in db.Sedes on l.SedeId equals s.Id
            orderby l.FechaFin, l.Producto
            select new
            {
                l.Id,
                l.Producto,
                l.Proveedor,
                l.Puestos,
                Equipos = l.Equipos.Count,
                l.FechaInicio,
                l.FechaFin,
                l.DocumentoCompra,
                l.Archivada,
                SedeNombre = s.Nombre,
            })
            .Take(MaximoResultados)
            .ToListAsync(cancellationToken);

        return filas
            .Select(f => new LicenciaDto(
                f.Id, f.Producto, f.Proveedor, f.Puestos, f.Equipos, f.FechaInicio, f.FechaFin, f.DocumentoCompra,
                ReglasVigencia.Calcular(f.FechaFin, f.Archivada, hoy), ReglasVigencia.DiasRestantes(f.FechaFin, hoy),
                f.Equipos > f.Puestos, f.SedeNombre))
            .ToList();
    }
}

internal sealed class ObtenerCifrasLicenciasHandler(IAppDbContext db, TimeProvider reloj) : IQueryHandler<ObtenerCifrasLicenciasQuery, CifrasLicenciasDto>
{
    public async Task<Result<CifrasLicenciasDto>> Handle(ObtenerCifrasLicenciasQuery query, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var aviso = hoy.AddDays(ReglasVigencia.DiasAviso);

        var licencias = db.Licencias.AsNoTracking().Where(l => !l.Archivada);
        if (query.SedeId is Guid sedeId)
        {
            licencias = licencias.Where(l => l.SedeId == sedeId);
        }

        return new CifrasLicenciasDto(
            await licencias.CountAsync(l => l.FechaFin >= hoy, cancellationToken),
            await licencias.CountAsync(l => l.FechaFin >= hoy && l.FechaFin <= aviso, cancellationToken),
            await licencias.CountAsync(l => l.FechaFin < hoy, cancellationToken),
            await licencias.CountAsync(l => l.Equipos.Count > l.Puestos, cancellationToken),
            await licencias
                .Where(l => l.FechaFin >= hoy && l.Puestos > l.Equipos.Count)
                .Select(l => l.Puestos - l.Equipos.Count)
                .SumAsync(cancellationToken),
            ReglasVigencia.DiasAviso);
    }
}

internal sealed class ObtenerLicenciaHandler(IAppDbContext db, TimeProvider reloj) : IQueryHandler<ObtenerLicenciaQuery, FichaLicenciaDto>
{
    public async Task<Result<FichaLicenciaDto>> Handle(ObtenerLicenciaQuery query, CancellationToken cancellationToken)
    {
        var licencia = await (
            from l in db.Licencias.AsNoTracking()
            join s in db.Sedes on l.SedeId equals s.Id
            where l.Id == query.Id
            select new { Licencia = l, SedeNombre = s.Nombre })
            .FirstOrDefaultAsync(cancellationToken);
        if (licencia is null)
        {
            return ErroresLicencia.NoEncontrada;
        }

        var equipos = await (
            from l in db.Licencias.AsNoTracking()
            where l.Id == query.Id
            from e in l.Equipos
            join a in db.Activos on e.ActivoId equals a.Id
            join t in db.TiposActivo on a.TipoActivoId equals t.Id
            join m in db.Marcas on a.MarcaId equals m.Id
            join es in db.Establecimientos on a.EstablecimientoId equals es.Id
            join st in db.EstadosActivo on a.EstadoActivoId equals st.Id
            orderby e.AsignadoEn descending, a.CodigoPatrimonial
            select new EquipoLicenciaDto(
                a.Id,
                a.CodigoPatrimonial,
                t.Nombre,
                m.Nombre,
                a.Modelo,
                a.NombreEquipo,
                es.Nombre,
                db.Asignaciones
                    .Where(x => x.ActivoId == a.Id && x.FechaDevolucion == null)
                    .Join(db.Personal, x => x.PersonaId, p => p.Id, (x, p) => p.NombreCompleto)
                    .FirstOrDefault(),
                st.Nombre,
                st.GrupoAsignado ?? GrupoEstado.EnUso,
                e.AsignadoEn))
            .ToListAsync(cancellationToken);

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var d = licencia.Licencia;
        return new FichaLicenciaDto(
            d.Id, d.SedeId, licencia.SedeNombre, d.Producto, d.Proveedor, d.Puestos, d.FechaInicio, d.FechaFin, d.DocumentoCompra,
            d.Costo, d.Observaciones, ReglasVigencia.Calcular(d.FechaFin, d.Archivada, hoy), ReglasVigencia.DiasRestantes(d.FechaFin, hoy),
            equipos, d.CreadoEn, d.ModificadoEn);
    }
}

internal sealed class ObtenerLicenciasActivoHandler(IAppDbContext db, TimeProvider reloj)
    : IQueryHandler<ObtenerLicenciasActivoQuery, IReadOnlyList<LicenciaDelActivoDto>>
{
    public async Task<Result<IReadOnlyList<LicenciaDelActivoDto>>> Handle(ObtenerLicenciasActivoQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Activos.AnyAsync(a => a.Id == query.ActivoId, cancellationToken))
        {
            return ErroresActivo.NoEncontrado;
        }

        var filas = await (
            from l in db.Licencias.AsNoTracking()
            where !l.Archivada
            from e in l.Equipos
            where e.ActivoId == query.ActivoId
            orderby l.FechaFin, l.Producto
            select new { l.Id, l.Producto, l.FechaFin, e.AsignadoEn })
            .ToListAsync(cancellationToken);

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        return filas
            .Select(f => new LicenciaDelActivoDto(
                f.Id, f.Producto, f.FechaFin, ReglasVigencia.Calcular(f.FechaFin, archivada: false, hoy),
                ReglasVigencia.DiasRestantes(f.FechaFin, hoy), f.AsignadoEn))
            .ToList();
    }
}

internal sealed class ListarProductosLicenciaHandler(IAppDbContext db) : IQueryHandler<ListarProductosLicenciaQuery, IReadOnlyList<string>>
{
    public const int Maximo = 200;

    public async Task<Result<IReadOnlyList<string>>> Handle(ListarProductosLicenciaQuery query, CancellationToken cancellationToken)
    {
        var productos = await db.Licencias.AsNoTracking()
            .Select(l => l.Producto)
            .Distinct()
            .OrderBy(p => p)
            .Take(Maximo)
            .ToListAsync(cancellationToken);
        return productos;
    }
}
