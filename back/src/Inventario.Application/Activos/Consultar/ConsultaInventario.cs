using Inventario.Application.Common.Persistencia;
using Inventario.Domain.Activos;

namespace Inventario.Application.Activos.Consultar;

/// <summary>Filtros del inventario: los mismos para la lista y para la exportación a Excel.</summary>
/// <param name="SedeId">Solo para el superadministrador.</param>
/// <param name="EstablecimientoId">Establecimiento.</param>
/// <param name="TipoActivoId">Tipo de activo.</param>
/// <param name="AreaId">Área.</param>
/// <param name="EstadoActivoId">Estado.</param>
/// <param name="Garantia">Estado de la garantía hoy.</param>
/// <param name="Renovacion">Situación de renovación hoy.</param>
/// <param name="Atencion">Requieren atención por ese motivo (sin los de baja).</param>
/// <param name="Falta">Les falta ese dato (sin los de baja).</param>
/// <param name="Buscar">Código, serie, modelo, marca, IP, nombre de equipo, persona asignada (nombre o DNI) o responsable anterior.</param>
public sealed record FiltrosInventario(
    Guid? SedeId,
    Guid? EstablecimientoId,
    Guid? TipoActivoId,
    Guid? AreaId,
    Guid? EstadoActivoId,
    EstadoGarantia? Garantia,
    SituacionRenovacion? Renovacion,
    MotivoAtencion? Atencion,
    DatoFaltante? Falta,
    string? Buscar);

/// <summary>
/// Un activo del inventario con los datos de la lista, como fila de consulta. Tiene propiedades (no constructor) para
/// que EF Core pueda filtrar y ordenar por ellas antes de traer los datos.
/// </summary>
internal sealed class FilaInventario
{
    public Guid Id { get; init; }

    public string CodigoPatrimonial { get; init; } = string.Empty;

    public string TipoActivo { get; init; } = string.Empty;

    public string Marca { get; init; } = string.Empty;

    public string Modelo { get; init; } = string.Empty;

    public string? NumeroSerie { get; init; }

    public string? NombreEquipo { get; init; }

    public string? DireccionIp { get; init; }

    public string Establecimiento { get; init; } = string.Empty;

    public string Area { get; init; } = string.Empty;

    /// <summary>Texto escrito antes del módulo de asignaciones (D52).</summary>
    public string? ResponsableAnterior { get; init; }

    /// <summary>Quién lo tiene asignado (D51).</summary>
    public string? Persona { get; init; }

    public string? PersonaDni { get; init; }

    public string Estado { get; init; } = string.Empty;

    public DateOnly? FinGarantia { get; init; }

    public DateOnly? FechaRenovacion { get; init; }

    public Guid SedeId { get; init; }

    public string SedeNombre { get; init; } = string.Empty;
}

/// <summary>Consulta del inventario con sus filtros, compartida por la lista y la exportación.</summary>
internal static class ConsultaInventario
{
    /// <summary>Activos que cumplen los filtros. El filtro global de sede ya limita a un usuario de sede a la suya.</summary>
    public static IQueryable<FilaInventario> Filas(IAppDbContext db, FiltrosInventario filtros, DateOnly hoy)
    {
        var enUnAnio = hoy.AddYears(ListasActivo.AniosAvisoRenovacion);
        var activos = db.Activos.AsQueryable();

        if (filtros.SedeId is Guid sedeId)
        {
            activos = activos.Where(a => a.SedeId == sedeId);
        }

        if (filtros.EstablecimientoId is Guid establecimientoId)
        {
            activos = activos.Where(a => a.EstablecimientoId == establecimientoId);
        }

        if (filtros.TipoActivoId is Guid tipoId)
        {
            activos = activos.Where(a => a.TipoActivoId == tipoId);
        }

        if (filtros.AreaId is Guid areaId)
        {
            activos = activos.Where(a => a.AreaId == areaId);
        }

        if (filtros.EstadoActivoId is Guid estadoId)
        {
            activos = activos.Where(a => a.EstadoActivoId == estadoId);
        }

        activos = filtros.Garantia switch
        {
            EstadoGarantia.SinDato => activos.Where(a => a.FinGarantia == null),
            EstadoGarantia.Vigente => activos.Where(a => a.FinGarantia >= hoy),
            EstadoGarantia.Vencida => activos.Where(a => a.FinGarantia < hoy),
            _ => activos,
        };

        activos = filtros.Renovacion switch
        {
            SituacionRenovacion.SinDato => activos.Where(a => a.FechaRenovacion == null),
            SituacionRenovacion.Renovar => activos.Where(a => a.FechaRenovacion <= hoy),
            SituacionRenovacion.ProximoARenovar => activos.Where(a => a.FechaRenovacion > hoy && a.FechaRenovacion <= enUnAnio),
            SituacionRenovacion.Vigente => activos.Where(a => a.FechaRenovacion > enUnAnio),
            _ => activos,
        };

        if (filtros.Atencion is MotivoAtencion motivo)
        {
            activos = activos.ConMotivo(db, motivo, hoy);
        }

        if (filtros.Falta is DatoFaltante falta)
        {
            activos = activos.ConFalta(db, falta);
        }

        var filas =
            from a in activos
            join t in db.TiposActivo on a.TipoActivoId equals t.Id
            join m in db.Marcas on a.MarcaId equals m.Id
            join ar in db.Areas on a.AreaId equals ar.Id
            join es in db.EstadosActivo on a.EstadoActivoId equals es.Id
            join e in db.Establecimientos on a.EstablecimientoId equals e.Id
            join s in db.Sedes on a.SedeId equals s.Id
            // La asignación vigente (a lo sumo una, D51) y su persona.
            join v in db.Asignaciones.Where(x => x.FechaDevolucion == null) on a.Id equals v.ActivoId into vigentes
            from v in vigentes.DefaultIfEmpty()
            join p in db.Personal on v!.PersonaId equals p.Id into personas
            from p in personas.DefaultIfEmpty()
            select new FilaInventario
            {
                Id = a.Id,
                CodigoPatrimonial = a.CodigoPatrimonial,
                TipoActivo = t.Nombre,
                Marca = m.Nombre,
                Modelo = a.Modelo,
                NumeroSerie = a.NumeroSerie,
                NombreEquipo = a.NombreEquipo,
                DireccionIp = a.DireccionIp,
                Establecimiento = e.Nombre,
                Area = ar.Nombre,
                ResponsableAnterior = a.Responsable,
                Persona = p != null ? p.NombreCompleto : null,
                PersonaDni = p != null ? p.Dni : null,
                Estado = es.Nombre,
                FinGarantia = a.FinGarantia,
                FechaRenovacion = a.FechaRenovacion,
                SedeId = a.SedeId,
                SedeNombre = s.Nombre,
            };

        var buscar = filtros.Buscar?.Trim();
        if (!string.IsNullOrEmpty(buscar))
        {
            filas = filas.Where(f =>
                f.CodigoPatrimonial.Contains(buscar)
                || (f.NumeroSerie != null && f.NumeroSerie.Contains(buscar))
                || f.Modelo.Contains(buscar)
                || (f.Persona != null && f.Persona.Contains(buscar))
                || (f.PersonaDni != null && f.PersonaDni.StartsWith(buscar))
                || (f.ResponsableAnterior != null && f.ResponsableAnterior.Contains(buscar))
                || f.Marca.Contains(buscar)
                || (f.DireccionIp != null && f.DireccionIp.Contains(buscar))
                || (f.NombreEquipo != null && f.NombreEquipo.Contains(buscar)));
        }

        return filas;
    }
}
