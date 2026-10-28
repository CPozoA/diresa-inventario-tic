using Inventario.Application.Activos.Consultar;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Establecimientos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Resumen;

/// <summary>Las cuatro cifras de arriba.</summary>
/// <param name="Activos">Todos los activos registrados, incluidos los de baja.</param>
/// <param name="EnUso">Los de un estado del grupo "en uso" (Operativo).</param>
/// <param name="Renovar">Vida útil agotada (sin los de baja, D49).</param>
/// <param name="ProximosARenovar">En su último año de vida útil (sin los de baja).</param>
/// <param name="GarantiaVencida">Garantía vencida (sin los de baja).</param>
public sealed record CifrasResumenDto(int Activos, int EnUso, int Renovar, int ProximosARenovar, int GarantiaVencida);

/// <summary>Cuántos activos hay en un estado.</summary>
public sealed record CantidadPorEstadoDto(Guid EstadoActivoId, string Nombre, GrupoEstado Grupo, int Cantidad);

/// <summary>Años desde la fecha de adquisición.</summary>
public enum RangoAntiguedad
{
    MenosDe2,
    De2A4,
    De4A6,
    MasDe6,
    SinFecha,
}

/// <summary>Cuántos activos (sin los de baja) hay en un rango de antigüedad.</summary>
public sealed record CantidadPorAntiguedadDto(RangoAntiguedad Rango, int Cantidad);

/// <summary>Cuántos activos hay de un tipo.</summary>
public sealed record CantidadPorTipoDto(Guid TipoActivoId, string Nombre, int Cantidad);

/// <summary>Un establecimiento con sus cifras.</summary>
/// <param name="EstablecimientoId">Establecimiento.</param>
/// <param name="Nombre">Nombre.</param>
/// <param name="Activos">Activos registrados.</param>
/// <param name="EnUso">Los que están en uso.</param>
/// <param name="Renovar">Los de vida útil agotada (sin los de baja).</param>
public sealed record CifrasEstablecimientoDto(Guid EstablecimientoId, string Nombre, int Activos, int EnUso, int Renovar);

/// <summary>Establecimientos de la red (los vigentes, D39).</summary>
/// <param name="Total">Establecimientos vigentes.</param>
/// <param name="SinActivos">Vigentes sin ningún activo registrado: la red todavía no los inventarió.</param>
/// <param name="Principales">Los que tienen más activos (hasta 6).</param>
public sealed record EstablecimientosResumenDto(int Total, int SinActivos, IReadOnlyList<CifrasEstablecimientoDto> Principales);

/// <summary>Un activo que requiere atención.</summary>
public sealed record ActivoAtencionDto(
    Guid Id,
    string CodigoPatrimonial,
    string TipoActivo,
    string Marca,
    string Modelo,
    string Establecimiento,
    string Estado,
    MotivoAtencion Motivo,
    DateOnly? FechaAdquisicion,
    int? VidaUtilAnios,
    DateOnly? FechaRenovacion);

/// <summary>Activos que requieren atención: primero los fuera de servicio, luego para renovar, próximos y en mantenimiento.</summary>
/// <param name="Total">Todos los que la requieren (para "Ver los …").</param>
/// <param name="Primeros">Los primeros (hasta 6).</param>
public sealed record AtencionResumenDto(int Total, IReadOnlyList<ActivoAtencionDto> Primeros);

/// <summary>Fichas por completar, sin contar los activos de baja.</summary>
/// <param name="Evaluadas">Activos que no están de baja.</param>
/// <param name="Completas">Con número de serie, fecha de adquisición, al menos una foto y asignada (salvo en almacén).</param>
/// <param name="SinFotos">Sin ninguna foto.</param>
/// <param name="SinFechaAdquisicion">Sin fecha de adquisición (no se calcula su renovación).</param>
/// <param name="SinResponsable">Sin asignación vigente, sin contar los que están en almacén (D52).</param>
/// <param name="SinNumeroSerie">Sin número de serie.</param>
public sealed record FichasResumenDto(int Evaluadas, int Completas, int SinFotos, int SinFechaAdquisicion, int SinResponsable, int SinNumeroSerie);

/// <summary>Una red en la tabla del superadministrador.</summary>
public sealed record CifrasRedDto(
    Guid SedeId,
    string Nombre,
    int Activos,
    int EnUso,
    int Renovar,
    int ProximosARenovar,
    int GarantiaVencida,
    int FichasEvaluadas,
    int FichasCompletas,
    int Establecimientos,
    int EstablecimientosSinActivos);

/// <summary>Resumen del inicio (docs/ARQUITECTURA.md §5.11).</summary>
/// <param name="Fecha">Fecha de las cifras (hoy).</param>
/// <param name="SedeId">Red resumida; nula = todas (solo el superadministrador).</param>
/// <param name="SedeNombre">Nombre de la red resumida; nulo = todas.</param>
/// <param name="Cifras">Las cuatro cifras de arriba.</param>
/// <param name="Estados">Activos por estado, ordenados por grupo.</param>
/// <param name="Antiguedad">Activos por antigüedad, sin los de baja.</param>
/// <param name="Tipos">Activos por tipo, de más a menos.</param>
/// <param name="Establecimientos">Establecimientos con y sin activos.</param>
/// <param name="Atencion">Activos que requieren atención.</param>
/// <param name="Fichas">Fichas por completar.</param>
/// <param name="Redes">Solo para el superadministrador con todas las redes: una fila por red.</param>
public sealed record ResumenDto(
    DateOnly Fecha,
    Guid? SedeId,
    string? SedeNombre,
    CifrasResumenDto Cifras,
    IReadOnlyList<CantidadPorEstadoDto> Estados,
    IReadOnlyList<CantidadPorAntiguedadDto> Antiguedad,
    IReadOnlyList<CantidadPorTipoDto> Tipos,
    EstablecimientosResumenDto Establecimientos,
    AtencionResumenDto Atencion,
    FichasResumenDto Fichas,
    IReadOnlyList<CifrasRedDto>? Redes);

/// <summary>
/// Resumen del inicio. Un usuario de sede ve su red; el superadministrador ve todas (con la tabla por red) o la que
/// indique. Los activos de baja cuentan en el total y en los estados, pero no en las alertas ni en las fichas (D49).
/// </summary>
/// <param name="SedeId">Solo para el superadministrador: resume una red.</param>
/// <remarks>Requiere Activos.Ver.</remarks>
public sealed record ObtenerResumenQuery(Guid? SedeId) : IQuery<ResumenDto>;

internal sealed class ObtenerResumenHandler(IAppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
    : IQueryHandler<ObtenerResumenQuery, ResumenDto>
{
    private const int MaximoEnLista = 6;

    /// <summary>Orden de "Requieren atención".</summary>
    private static readonly MotivoAtencion[] Prioridad =
        [MotivoAtencion.FueraDeServicio, MotivoAtencion.Renovar, MotivoAtencion.ProximoARenovar, MotivoAtencion.EnMantenimiento];

    public async Task<Result<ResumenDto>> Handle(ObtenerResumenQuery query, CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

        // Un usuario de sede siempre ve la suya (el filtro global ya lo limita); el parámetro es para el superadministrador.
        var sedeId = usuario.EsSuperAdmin ? query.SedeId : null;
        string? sedeNombre = null;
        if (sedeId is Guid id)
        {
            sedeNombre = await db.Sedes.AsNoTracking().Where(s => s.Id == id).Select(s => s.Nombre).FirstOrDefaultAsync(cancellationToken);
            if (sedeNombre is null)
            {
                return ErrorAplicacion.NoEncontrado("Resumen.SedeNoEncontrada", "La red no existe.");
            }
        }
        else if (!usuario.EsSuperAdmin && usuario.SedeId is Guid propia)
        {
            sedeId = propia;
            sedeNombre = await db.Sedes.AsNoTracking().Where(s => s.Id == propia).Select(s => s.Nombre).FirstOrDefaultAsync(cancellationToken);
        }

        var activos = db.Activos.AsNoTracking();
        var establecimientos = db.Establecimientos.AsNoTracking().Where(e => e.Activo);
        if (sedeId is Guid sede)
        {
            activos = activos.Where(a => a.SedeId == sede);
            establecimientos = establecimientos.Where(e => e.SedeId == sede);
        }

        var cifras = new CifrasResumenDto(
            await activos.CountAsync(cancellationToken),
            await activos.EnGrupo(db, GrupoEstado.EnUso).CountAsync(cancellationToken),
            await activos.ConMotivo(db, MotivoAtencion.Renovar, hoy).CountAsync(cancellationToken),
            await activos.ConMotivo(db, MotivoAtencion.ProximoARenovar, hoy).CountAsync(cancellationToken),
            await activos.ConMotivo(db, MotivoAtencion.GarantiaVencida, hoy).CountAsync(cancellationToken));

        var resumen = new ResumenDto(
            hoy,
            sedeId,
            sedeNombre,
            cifras,
            await EstadosAsync(activos, cancellationToken),
            await AntiguedadAsync(activos, hoy, cancellationToken),
            await TiposAsync(activos, cancellationToken),
            await EstablecimientosAsync(activos, establecimientos, hoy, cancellationToken),
            await AtencionAsync(activos, hoy, cancellationToken),
            await FichasAsync(activos, cancellationToken),
            usuario.EsSuperAdmin && sedeId is null ? await RedesAsync(hoy, cancellationToken) : null);

        return resumen;
    }

    private async Task<IReadOnlyList<CantidadPorEstadoDto>> EstadosAsync(IQueryable<Activo> activos, CancellationToken ct)
    {
        var porEstado = await activos
            .GroupBy(a => a.EstadoActivoId)
            .Select(g => new { EstadoId = g.Key, Cantidad = g.Count() })
            .ToListAsync(ct);
        var ids = porEstado.Select(x => x.EstadoId).ToList();
        var estados = await db.EstadosActivo.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.Nombre, Grupo = e.GrupoAsignado ?? GrupoEstado.EnUso })
            .ToDictionaryAsync(e => e.Id, ct);

        return porEstado
            .Where(x => estados.ContainsKey(x.EstadoId))
            .Select(x => new CantidadPorEstadoDto(x.EstadoId, estados[x.EstadoId].Nombre, estados[x.EstadoId].Grupo, x.Cantidad))
            .OrderBy(x => x.Grupo)
            .ThenBy(x => x.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }

    private async Task<IReadOnlyList<CantidadPorAntiguedadDto>> AntiguedadAsync(IQueryable<Activo> activos, DateOnly hoy, CancellationToken ct)
    {
        var vigentes = activos.SinBaja(db);
        var hace2 = hoy.AddYears(-2);
        var hace4 = hoy.AddYears(-4);
        var hace6 = hoy.AddYears(-6);

        return
        [
            new(RangoAntiguedad.MenosDe2, await vigentes.CountAsync(a => a.FechaAdquisicion > hace2, ct)),
            new(RangoAntiguedad.De2A4, await vigentes.CountAsync(a => a.FechaAdquisicion <= hace2 && a.FechaAdquisicion > hace4, ct)),
            new(RangoAntiguedad.De4A6, await vigentes.CountAsync(a => a.FechaAdquisicion <= hace4 && a.FechaAdquisicion > hace6, ct)),
            new(RangoAntiguedad.MasDe6, await vigentes.CountAsync(a => a.FechaAdquisicion <= hace6, ct)),
            new(RangoAntiguedad.SinFecha, await vigentes.CountAsync(a => a.FechaAdquisicion == null, ct)),
        ];
    }

    private async Task<IReadOnlyList<CantidadPorTipoDto>> TiposAsync(IQueryable<Activo> activos, CancellationToken ct)
    {
        var cantidades = await ContarPor(activos, a => a.TipoActivoId, ct);
        var ids = cantidades.Keys.ToList();
        var nombres = await db.TiposActivo.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Nombre, ct);

        return cantidades
            .Where(x => nombres.ContainsKey(x.Key))
            .Select(x => new CantidadPorTipoDto(x.Key, nombres[x.Key], x.Value))
            .OrderByDescending(x => x.Cantidad)
            .ThenBy(x => x.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }

    private async Task<EstablecimientosResumenDto> EstablecimientosAsync(
        IQueryable<Activo> activos,
        IQueryable<Establecimiento> establecimientos,
        DateOnly hoy,
        CancellationToken ct)
    {
        var total = await establecimientos.CountAsync(ct);
        var sinActivos = await establecimientos.CountAsync(e => !db.Activos.Any(a => a.EstablecimientoId == e.Id), ct);

        var porEstablecimiento = await activos
            .GroupBy(a => a.EstablecimientoId)
            .Select(g => new { EstablecimientoId = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad)
            .Take(MaximoEnLista)
            .ToListAsync(ct);

        var ids = porEstablecimiento.Select(x => x.EstablecimientoId).ToList();
        var nombres = await db.Establecimientos.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Nombre, ct);
        var principales = porEstablecimiento
            .Select(x => new { x.EstablecimientoId, Nombre = nombres.GetValueOrDefault(x.EstablecimientoId, string.Empty), x.Cantidad })
            .ToList();
        var enUso = await ContarPor(activos.EnGrupo(db, GrupoEstado.EnUso).Where(a => ids.Contains(a.EstablecimientoId)), a => a.EstablecimientoId, ct);
        var renovar = await ContarPor(
            activos.ConMotivo(db, MotivoAtencion.Renovar, hoy).Where(a => ids.Contains(a.EstablecimientoId)), a => a.EstablecimientoId, ct);

        var lista = principales
            .OrderByDescending(x => x.Cantidad)
            .ThenBy(x => x.Nombre, StringComparer.CurrentCulture)
            .Select(x => new CifrasEstablecimientoDto(
                x.EstablecimientoId,
                x.Nombre,
                x.Cantidad,
                enUso.GetValueOrDefault(x.EstablecimientoId),
                renovar.GetValueOrDefault(x.EstablecimientoId)))
            .ToList();

        return new EstablecimientosResumenDto(total, sinActivos, lista);
    }

    private async Task<AtencionResumenDto> AtencionAsync(IQueryable<Activo> activos, DateOnly hoy, CancellationToken ct)
    {
        var total = await activos.ConMotivo(db, MotivoAtencion.Cualquiera, hoy).CountAsync(ct);

        // Los primeros de cada motivo, en orden de prioridad; un activo aparece una sola vez, con su motivo más urgente.
        var primeros = new List<ActivoAtencionDto>();
        foreach (var motivo in Prioridad)
        {
            if (primeros.Count >= MaximoEnLista)
            {
                break;
            }

            var consulta = activos.ConMotivo(db, motivo, hoy);
            consulta = motivo is MotivoAtencion.Renovar or MotivoAtencion.ProximoARenovar
                ? consulta.OrderBy(a => a.FechaRenovacion).ThenBy(a => a.CodigoPatrimonial)
                : consulta.OrderBy(a => a.CodigoPatrimonial);

            var ya = primeros.Select(p => p.Id).ToList();
            var filas = await (
                from a in consulta.Where(a => !ya.Contains(a.Id)).Take(MaximoEnLista)
                join t in db.TiposActivo on a.TipoActivoId equals t.Id
                join m in db.Marcas on a.MarcaId equals m.Id
                join e in db.Establecimientos on a.EstablecimientoId equals e.Id
                join es in db.EstadosActivo on a.EstadoActivoId equals es.Id
                select new ActivoAtencionDto(
                    a.Id,
                    a.CodigoPatrimonial,
                    t.Nombre,
                    m.Nombre,
                    a.Modelo,
                    e.Nombre,
                    es.Nombre,
                    motivo,
                    a.FechaAdquisicion,
                    a.VidaUtilAnios,
                    a.FechaRenovacion)).ToListAsync(ct);

            // El join no conserva el orden: se vuelve a ordenar igual que la consulta.
            primeros.AddRange((motivo is MotivoAtencion.Renovar or MotivoAtencion.ProximoARenovar
                    ? filas.OrderBy(f => f.FechaRenovacion).ThenBy(f => f.CodigoPatrimonial, StringComparer.Ordinal)
                    : filas.OrderBy(f => f.CodigoPatrimonial, StringComparer.Ordinal))
                .Take(MaximoEnLista - primeros.Count));
        }

        return new AtencionResumenDto(total, primeros);
    }

    private async Task<FichasResumenDto> FichasAsync(IQueryable<Activo> activos, CancellationToken ct) =>
        new(
            await activos.SinBaja(db).CountAsync(ct),
            await activos.Completas(db).CountAsync(ct),
            await activos.ConFalta(db, DatoFaltante.Fotos).CountAsync(ct),
            await activos.ConFalta(db, DatoFaltante.FechaAdquisicion).CountAsync(ct),
            await activos.ConFalta(db, DatoFaltante.Responsable).CountAsync(ct),
            await activos.ConFalta(db, DatoFaltante.NumeroSerie).CountAsync(ct));

    /// <summary>Tabla por red del superadministrador: cada cifra se cuenta por sede en una sola consulta.</summary>
    private async Task<IReadOnlyList<CifrasRedDto>> RedesAsync(DateOnly hoy, CancellationToken ct)
    {
        var activos = db.Activos.AsNoTracking();
        var establecimientos = db.Establecimientos.AsNoTracking().Where(e => e.Activo);

        var total = await ContarPor(activos, a => a.SedeId, ct);
        var enUso = await ContarPor(activos.EnGrupo(db, GrupoEstado.EnUso), a => a.SedeId, ct);
        var renovar = await ContarPor(activos.ConMotivo(db, MotivoAtencion.Renovar, hoy), a => a.SedeId, ct);
        var proximos = await ContarPor(activos.ConMotivo(db, MotivoAtencion.ProximoARenovar, hoy), a => a.SedeId, ct);
        var garantia = await ContarPor(activos.ConMotivo(db, MotivoAtencion.GarantiaVencida, hoy), a => a.SedeId, ct);
        var evaluadas = await ContarPor(activos.SinBaja(db), a => a.SedeId, ct);
        var completas = await ContarPor(activos.Completas(db), a => a.SedeId, ct);
        var estTotal = await establecimientos
            .GroupBy(e => e.SedeId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var estSin = await establecimientos
            .Where(e => !db.Activos.Any(a => a.EstablecimientoId == e.Id))
            .GroupBy(e => e.SedeId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);

        var sedes = await db.Sedes.AsNoTracking().Where(s => s.Activa).Select(s => new { s.Id, s.Nombre }).ToListAsync(ct);

        return sedes
            .Select(s => new CifrasRedDto(
                s.Id,
                s.Nombre,
                total.GetValueOrDefault(s.Id),
                enUso.GetValueOrDefault(s.Id),
                renovar.GetValueOrDefault(s.Id),
                proximos.GetValueOrDefault(s.Id),
                garantia.GetValueOrDefault(s.Id),
                evaluadas.GetValueOrDefault(s.Id),
                completas.GetValueOrDefault(s.Id),
                estTotal.GetValueOrDefault(s.Id),
                estSin.GetValueOrDefault(s.Id)))
            .OrderByDescending(r => r.Activos)
            .ThenBy(r => r.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }

    private static Task<Dictionary<Guid, int>> ContarPor(
        IQueryable<Activo> activos,
        System.Linq.Expressions.Expression<Func<Activo, Guid>> clave,
        CancellationToken ct) =>
        activos.GroupBy(clave).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
}
