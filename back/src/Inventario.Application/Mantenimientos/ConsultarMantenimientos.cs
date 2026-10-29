using Inventario.Application.Activos;
using Inventario.Application.Activos.Consultar;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Mantenimientos;
using Inventario.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Mantenimientos;

/// <summary>Qué mantenimientos listar.</summary>
public enum VistaMantenimientos
{
    /// <summary>Abiertos, el más antiguo primero.</summary>
    EnCurso,

    /// <summary>Cerrados, el más reciente primero.</summary>
    Historial,
}

/// <summary>Mantenimientos de la red, paginados.</summary>
/// <param name="Vista">En curso o historial.</param>
/// <param name="SedeId">Solo para el superadministrador.</param>
/// <param name="Buscar">Código patrimonial, modelo, marca, proveedor, orden de servicio o técnico.</param>
/// <param name="Tipo">Preventivo o correctivo.</param>
/// <param name="EstablecimientoId">Establecimiento del activo.</param>
/// <param name="Pagina">Desde 1.</param>
/// <param name="TamanoPagina">Entre 1 y 100 (por omisión, 25).</param>
/// <remarks>Requiere Mantenimiento.Ver.</remarks>
public sealed record ListarMantenimientosQuery(
    VistaMantenimientos Vista,
    Guid? SedeId,
    string? Buscar,
    TipoMantenimiento? Tipo,
    Guid? EstablecimientoId,
    int? Pagina,
    int? TamanoPagina) : IQuery<PaginaMantenimientosDto>;

/// <summary>
/// Activos con el preventivo vencido o en los próximos días, el más atrasado primero. No incluye los de baja ni los que
/// ya tienen un mantenimiento en curso.
/// </summary>
/// <param name="SedeId">Solo para el superadministrador.</param>
/// <param name="EstablecimientoId">Establecimiento del activo.</param>
/// <param name="Buscar">Código patrimonial, modelo o marca.</param>
/// <param name="Dias">Cuántos días hacia adelante (por omisión, 30; hasta 365).</param>
/// <remarks>Requiere Mantenimiento.Ver.</remarks>
public sealed record ListarProgramadosQuery(Guid? SedeId, Guid? EstablecimientoId, string? Buscar, int? Dias) : IQuery<IReadOnlyList<ProgramadoDto>>;

/// <summary>Cifras de la página de mantenimiento: en curso, preventivos vencidos y próximos.</summary>
/// <remarks>Requiere Mantenimiento.Ver.</remarks>
public sealed record ObtenerCifrasMantenimientoQuery(Guid? SedeId, int? Dias) : IQuery<CifrasMantenimientoDto>;

/// <summary>Mantenimiento de un activo para su ficha: el en curso, el último y el próximo preventivo, y el historial.</summary>
/// <remarks>Requiere Mantenimiento.Ver. Un activo de otra sede "no existe".</remarks>
public sealed record ObtenerMantenimientosActivoQuery(Guid ActivoId) : IQuery<MantenimientosActivoDto>;

/// <summary>Técnicos de la red: usuarios activos con permiso para gestionar mantenimientos.</summary>
/// <param name="SedeId">Red del activo (el superadministrador la indica; un usuario de sede recibe la suya).</param>
/// <remarks>Requiere Mantenimiento.Gestionar.</remarks>
public sealed record ListarTecnicosQuery(Guid? SedeId) : IQuery<IReadOnlyList<TecnicoDto>>;

internal sealed class ListarMantenimientosHandler(IAppDbContext db) : IQueryHandler<ListarMantenimientosQuery, PaginaMantenimientosDto>
{
    public const int TamanoPorOmision = 25;
    public const int TamanoMaximo = 100;

    public async Task<Result<PaginaMantenimientosDto>> Handle(ListarMantenimientosQuery query, CancellationToken cancellationToken)
    {
        var pagina = Math.Max(1, query.Pagina ?? 1);
        var tamano = Math.Clamp(query.TamanoPagina ?? TamanoPorOmision, 1, TamanoMaximo);

        // El filtro global de sede ya limita a un usuario de sede a la suya.
        var mantenimientos = db.Mantenimientos.AsNoTracking();
        mantenimientos = query.Vista == VistaMantenimientos.EnCurso
            ? mantenimientos.Where(m => m.FechaFin == null)
            : mantenimientos.Where(m => m.FechaFin != null);

        if (query.SedeId is Guid sedeId)
        {
            mantenimientos = mantenimientos.Where(m => m.SedeId == sedeId);
        }

        if (query.Tipo is TipoMantenimiento tipo)
        {
            mantenimientos = mantenimientos.Where(m => m.Tipo == tipo);
        }

        var filas = ConsultasMantenimiento.Filas(db, mantenimientos);

        if (query.EstablecimientoId is Guid establecimientoId)
        {
            filas = filas.Where(f => f.EstablecimientoId == establecimientoId);
        }

        var buscar = query.Buscar?.Trim();
        if (!string.IsNullOrEmpty(buscar))
        {
            filas = filas.Where(f =>
                f.CodigoPatrimonial.Contains(buscar)
                || f.Modelo.Contains(buscar)
                || f.Marca.Contains(buscar)
                || (f.Proveedor != null && f.Proveedor.Contains(buscar))
                || (f.OrdenServicio != null && f.OrdenServicio.Contains(buscar))
                || (f.Tecnico != null && f.Tecnico.Contains(buscar)));
        }

        var total = await filas.CountAsync(cancellationToken);

        var ordenadas = query.Vista == VistaMantenimientos.EnCurso
            ? filas.OrderBy(f => f.FechaInicio).ThenBy(f => f.CreadoEn)
            : filas.OrderByDescending(f => f.FechaFin).ThenByDescending(f => f.CreadoEn);

        var lista = await ordenadas.Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(cancellationToken);
        return new PaginaMantenimientosDto(lista.Select(f => f.ADto()).ToList(), total, pagina, tamano);
    }
}

internal sealed class ListarProgramadosHandler(IAppDbContext db, TimeProvider reloj) : IQueryHandler<ListarProgramadosQuery, IReadOnlyList<ProgramadoDto>>
{
    public const int DiasPorOmision = 30;
    public const int MaximoResultados = 500;

    public async Task<Result<IReadOnlyList<ProgramadoDto>>> Handle(ListarProgramadosQuery query, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var hasta = hoy.AddDays(Math.Clamp(query.Dias ?? DiasPorOmision, 0, 365));

        var activos = ConsultasProgramados.Pendientes(db, db.Activos.AsNoTracking(), hasta);

        if (query.SedeId is Guid sedeId)
        {
            activos = activos.Where(a => a.SedeId == sedeId);
        }

        if (query.EstablecimientoId is Guid establecimientoId)
        {
            activos = activos.Where(a => a.EstablecimientoId == establecimientoId);
        }

        var buscar = query.Buscar?.Trim();

        var filas =
            from a in activos
            join t in db.TiposActivo on a.TipoActivoId equals t.Id
            join m in db.Marcas on a.MarcaId equals m.Id
            join e in db.Establecimientos on a.EstablecimientoId equals e.Id
            join s in db.Sedes on a.SedeId equals s.Id
            where string.IsNullOrEmpty(buscar)
                || a.CodigoPatrimonial.Contains(buscar) || a.Modelo.Contains(buscar) || m.Nombre.Contains(buscar)
            orderby a.ProximoMantenimiento, a.CodigoPatrimonial
            select new
            {
                a.Id,
                a.CodigoPatrimonial,
                TipoActivo = t.Nombre,
                Marca = m.Nombre,
                a.Modelo,
                Establecimiento = e.Nombre,
                UltimoPreventivo = db.Mantenimientos
                    .Where(x => x.ActivoId == a.Id && x.Tipo == TipoMantenimiento.Preventivo && x.FechaFin != null)
                    .Max(x => x.FechaFin),
                Proximo = a.ProximoMantenimiento!.Value,
                Responsable = db.Asignaciones
                    .Where(x => x.ActivoId == a.Id && x.FechaDevolucion == null)
                    .Join(db.Personal, x => x.PersonaId, p => p.Id, (x, p) => p.NombreCompleto)
                    .FirstOrDefault(),
                SedeNombre = s.Nombre,
            };

        var lista = await filas.Take(MaximoResultados).ToListAsync(cancellationToken);
        return lista
            .Select(f => new ProgramadoDto(
                f.Id, f.CodigoPatrimonial, f.TipoActivo, f.Marca, f.Modelo, f.Establecimiento, f.UltimoPreventivo, f.Proximo,
                f.Proximo < hoy, f.Responsable, f.SedeNombre))
            .ToList();
    }
}

internal sealed class ObtenerCifrasMantenimientoHandler(IAppDbContext db, TimeProvider reloj)
    : IQueryHandler<ObtenerCifrasMantenimientoQuery, CifrasMantenimientoDto>
{
    public async Task<Result<CifrasMantenimientoDto>> Handle(ObtenerCifrasMantenimientoQuery query, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var dias = Math.Clamp(query.Dias ?? ListarProgramadosHandler.DiasPorOmision, 0, 365);
        var hasta = hoy.AddDays(dias);

        var mantenimientos = db.Mantenimientos.AsNoTracking().Where(m => m.FechaFin == null);
        var activos = db.Activos.AsNoTracking();
        if (query.SedeId is Guid sedeId)
        {
            mantenimientos = mantenimientos.Where(m => m.SedeId == sedeId);
            activos = activos.Where(a => a.SedeId == sedeId);
        }

        var pendientes = ConsultasProgramados.Pendientes(db, activos, hasta);
        return new CifrasMantenimientoDto(
            await mantenimientos.CountAsync(cancellationToken),
            await pendientes.CountAsync(a => a.ProximoMantenimiento < hoy, cancellationToken),
            await pendientes.CountAsync(a => a.ProximoMantenimiento >= hoy, cancellationToken),
            dias);
    }
}

internal sealed class ObtenerMantenimientosActivoHandler(IAppDbContext db) : IQueryHandler<ObtenerMantenimientosActivoQuery, MantenimientosActivoDto>
{
    public async Task<Result<MantenimientosActivoDto>> Handle(ObtenerMantenimientosActivoQuery query, CancellationToken cancellationToken)
    {
        var activo = await db.Activos.AsNoTracking()
            .Where(a => a.Id == query.ActivoId)
            .Select(a => new { a.Id, a.ProximoMantenimiento })
            .FirstOrDefaultAsync(cancellationToken);
        if (activo is null)
        {
            return ErroresActivo.NoEncontrado;
        }

        var filas = await ConsultasMantenimiento.Filas(db, db.Mantenimientos.AsNoTracking().Where(m => m.ActivoId == activo.Id))
            .OrderByDescending(f => f.FechaFin == null)
            .ThenByDescending(f => f.FechaFin)
            .ThenByDescending(f => f.FechaInicio)
            .ToListAsync(cancellationToken);

        var enCurso = filas.FirstOrDefault(f => f.FechaFin is null);
        var historial = filas.Where(f => f.FechaFin is not null).ToList();
        var ultimoPreventivo = historial.Where(f => f.Tipo == TipoMantenimiento.Preventivo).Max(f => f.FechaFin);

        return new MantenimientosActivoDto(
            enCurso?.ADto(), ultimoPreventivo, activo.ProximoMantenimiento, historial.Select(f => f.ADto()).ToList());
    }
}

internal sealed class ListarTecnicosHandler(IAppDbContext db, IUsuarioActual usuario) : IQueryHandler<ListarTecnicosQuery, IReadOnlyList<TecnicoDto>>
{
    public async Task<Result<IReadOnlyList<TecnicoDto>>> Handle(ListarTecnicosQuery query, CancellationToken cancellationToken)
    {
        if ((usuario.SedeId ?? query.SedeId) is not Guid sedeId)
        {
            return Array.Empty<TecnicoDto>();
        }

        var tecnicos = await ConsultasProgramados.Tecnicos(db, sedeId)
            .OrderBy(u => u.Nombres)
            .ThenBy(u => u.Apellidos)
            .Select(u => new TecnicoDto(u.Id, u.Nombres + " " + u.Apellidos))
            .ToListAsync(cancellationToken);
        return tecnicos;
    }
}

internal static class ConsultasProgramados
{
    /// <summary>
    /// Activos con preventivo hasta la fecha indicada (vencidos incluidos), sin los de baja ni los que tienen un
    /// mantenimiento en curso.
    /// </summary>
    public static IQueryable<Domain.Activos.Activo> Pendientes(IAppDbContext db, IQueryable<Domain.Activos.Activo> activos, DateOnly hasta) =>
        activos.SinBaja(db).Where(a =>
            a.ProximoMantenimiento != null
            && a.ProximoMantenimiento <= hasta
            && !db.Mantenimientos.Any(m => m.ActivoId == a.Id && m.FechaFin == null));

    /// <summary>Usuarios activos de la red con permiso para gestionar mantenimientos (directo o como administradores).</summary>
    public static IQueryable<Domain.Usuarios.Usuario> Tecnicos(IAppDbContext db, Guid sedeId)
    {
        var roles = db.Roles
            .Where(r => r.Tipo == TipoRol.AdministradorSede
                || r.PermisosAsignados.Any(p => p.CodigoPermiso == Permisos.Mantenimiento.Gestionar))
            .Select(r => r.Id);
        return db.Usuarios.Where(u => u.SedeId == sedeId && u.Activo && u.Roles.Any(ur => roles.Contains(ur.RolId)));
    }
}
