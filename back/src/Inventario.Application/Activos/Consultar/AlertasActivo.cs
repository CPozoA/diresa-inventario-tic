using Inventario.Application.Common.Persistencia;
using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;

namespace Inventario.Application.Activos.Consultar;

/// <summary>
/// Por qué un activo requiere atención (resumen del inicio, D49). Los activos de baja nunca la requieren.
/// Como filtro del inventario, <see cref="Cualquiera"/> reúne los cuatro primeros motivos.
/// </summary>
public enum MotivoAtencion
{
    /// <summary>Fuera de servicio, para renovar, próximo a renovar o en mantenimiento.</summary>
    Cualquiera,

    /// <summary>Su estado es del grupo "fuera de servicio" (Inoperativo…).</summary>
    FueraDeServicio,

    /// <summary>Vida útil agotada.</summary>
    Renovar,

    /// <summary>En su último año de vida útil.</summary>
    ProximoARenovar,

    /// <summary>Su estado es del grupo "en mantenimiento".</summary>
    EnMantenimiento,

    /// <summary>La garantía ya venció (solo como filtro: no se lista como atención, son demasiados).</summary>
    GarantiaVencida,
}

/// <summary>Dato que le falta a la ficha de un activo (resumen del inicio, "Fichas por completar").</summary>
public enum DatoFaltante
{
    /// <summary>No tiene ninguna foto.</summary>
    Fotos,

    /// <summary>Sin fecha de adquisición no se calcula la antigüedad ni la renovación.</summary>
    FechaAdquisicion,

    /// <summary>
    /// Nadie lo tiene asignado (D52). No cuentan los que están en almacén: esos no tienen por qué tener responsable.
    /// El texto "Responsable" de antes del módulo de asignaciones no cuenta como asignación.
    /// </summary>
    Responsable,

    NumeroSerie,
}

/// <summary>Filtros que comparten el inventario y el resumen, para que sus cifras coincidan.</summary>
internal static class AlertasActivo
{
    /// <summary>Sin los activos de baja (D49): son los que pueden requerir atención o tener la ficha incompleta.</summary>
    public static IQueryable<Activo> SinBaja(this IQueryable<Activo> activos, IAppDbContext db) =>
        activos.Where(a => db.EstadosActivo.Any(e => e.Id == a.EstadoActivoId && (e.GrupoAsignado ?? GrupoEstado.EnUso) != GrupoEstado.DeBaja));

    public static IQueryable<Activo> EnGrupo(this IQueryable<Activo> activos, IAppDbContext db, GrupoEstado grupo) =>
        activos.Where(a => db.EstadosActivo.Any(e => e.Id == a.EstadoActivoId && (e.GrupoAsignado ?? GrupoEstado.EnUso) == grupo));

    /// <param name="activos">Activos.</param>
    /// <param name="db">Contexto (para el grupo del estado).</param>
    /// <param name="motivo">Motivo.</param>
    /// <param name="hoy">Fecha de hoy.</param>
    public static IQueryable<Activo> ConMotivo(this IQueryable<Activo> activos, IAppDbContext db, MotivoAtencion motivo, DateOnly hoy)
    {
        var enUnAnio = hoy.AddYears(ListasActivo.AniosAvisoRenovacion);
        var vigentes = activos.SinBaja(db);
        return motivo switch
        {
            MotivoAtencion.FueraDeServicio => vigentes.EnGrupo(db, GrupoEstado.FueraDeServicio),
            MotivoAtencion.EnMantenimiento => vigentes.EnGrupo(db, GrupoEstado.EnMantenimiento),
            MotivoAtencion.Renovar => vigentes.Where(a => a.FechaRenovacion <= hoy),
            MotivoAtencion.ProximoARenovar => vigentes.Where(a => a.FechaRenovacion > hoy && a.FechaRenovacion <= enUnAnio),
            MotivoAtencion.GarantiaVencida => vigentes.Where(a => a.FinGarantia < hoy),
            _ => vigentes.Where(a =>
                a.FechaRenovacion <= enUnAnio
                || db.EstadosActivo.Any(e => e.Id == a.EstadoActivoId
                    && ((e.GrupoAsignado ?? GrupoEstado.EnUso) == GrupoEstado.FueraDeServicio
                        || (e.GrupoAsignado ?? GrupoEstado.EnUso) == GrupoEstado.EnMantenimiento))),
        };
    }

    public static IQueryable<Activo> ConFalta(this IQueryable<Activo> activos, IAppDbContext db, DatoFaltante falta)
    {
        var vigentes = activos.SinBaja(db);
        return falta switch
        {
            DatoFaltante.Fotos => vigentes.Where(a => !db.FotosActivo.Any(f => f.ActivoId == a.Id)),
            DatoFaltante.FechaAdquisicion => vigentes.Where(a => a.FechaAdquisicion == null),
            DatoFaltante.Responsable => vigentes.SinAsignar(db).Where(a =>
                !db.EstadosActivo.Any(e => e.Id == a.EstadoActivoId && e.GrupoAsignado == GrupoEstado.EnAlmacen)),
            _ => vigentes.Where(a => a.NumeroSerie == null || a.NumeroSerie == string.Empty),
        };
    }

    /// <summary>
    /// Ficha completa: número de serie, fecha de adquisición, al menos una foto y alguien a cargo (salvo que esté en
    /// almacén).
    /// </summary>
    public static IQueryable<Activo> Completas(this IQueryable<Activo> activos, IAppDbContext db) =>
        activos.SinBaja(db).Where(a =>
            a.NumeroSerie != null && a.NumeroSerie != string.Empty
            && a.FechaAdquisicion != null
            && (db.Asignaciones.Any(x => x.ActivoId == a.Id && x.FechaDevolucion == null)
                || db.EstadosActivo.Any(e => e.Id == a.EstadoActivoId && e.GrupoAsignado == GrupoEstado.EnAlmacen))
            && db.FotosActivo.Any(f => f.ActivoId == a.Id));

    /// <summary>Los que no tienen una asignación vigente.</summary>
    public static IQueryable<Activo> SinAsignar(this IQueryable<Activo> activos, IAppDbContext db) =>
        activos.Where(a => !db.Asignaciones.Any(x => x.ActivoId == a.Id && x.FechaDevolucion == null));
}
