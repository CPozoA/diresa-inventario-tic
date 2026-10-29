using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Mantenimientos;

namespace Inventario.Application.Mantenimientos;

/// <summary>Un mantenimiento, con los datos del activo para las listas.</summary>
/// <param name="Id">Mantenimiento.</param>
/// <param name="ActivoId">Activo.</param>
/// <param name="CodigoPatrimonial">Código patrimonial.</param>
/// <param name="TipoActivo">Tipo de activo.</param>
/// <param name="Marca">Marca.</param>
/// <param name="Modelo">Modelo.</param>
/// <param name="Establecimiento">Establecimiento del activo.</param>
/// <param name="Tipo">Preventivo o correctivo.</param>
/// <param name="FechaInicio">Inicio.</param>
/// <param name="Detalle">Problema reportado (correctivo) o actividades previstas (preventivo).</param>
/// <param name="Ejecutor">Personal de TI o proveedor.</param>
/// <param name="TecnicoId">Técnico (personal de TI).</param>
/// <param name="Tecnico">Nombre del técnico.</param>
/// <param name="Proveedor">Empresa (proveedor).</param>
/// <param name="OrdenServicio">N° de orden de servicio.</param>
/// <param name="Costo">Costo en soles.</param>
/// <param name="FechaFin">Término; nulo mientras está en curso.</param>
/// <param name="TrabajoRealizado">Qué se hizo.</param>
/// <param name="Resultado">Cómo quedó el equipo.</param>
/// <param name="SedeNombre">Red (vista del superadministrador).</param>
public sealed record MantenimientoDto(
    Guid Id,
    Guid ActivoId,
    string CodigoPatrimonial,
    string TipoActivo,
    string Marca,
    string Modelo,
    string Establecimiento,
    TipoMantenimiento Tipo,
    DateOnly FechaInicio,
    string? Detalle,
    EjecutorMantenimiento Ejecutor,
    Guid? TecnicoId,
    string? Tecnico,
    string? Proveedor,
    string? OrdenServicio,
    decimal? Costo,
    DateOnly? FechaFin,
    string? TrabajoRealizado,
    ResultadoMantenimiento? Resultado,
    string SedeNombre);

/// <summary>Una página de mantenimientos y el total que cumple los filtros.</summary>
public sealed record PaginaMantenimientosDto(IReadOnlyList<MantenimientoDto> Mantenimientos, int Total, int Pagina, int TamanoPagina);

/// <summary>Un activo con su próximo preventivo (pestaña "Programados").</summary>
/// <param name="ActivoId">Activo.</param>
/// <param name="CodigoPatrimonial">Código patrimonial.</param>
/// <param name="TipoActivo">Tipo de activo.</param>
/// <param name="Marca">Marca.</param>
/// <param name="Modelo">Modelo.</param>
/// <param name="Establecimiento">Establecimiento.</param>
/// <param name="UltimoPreventivo">Término del último preventivo, si hubo.</param>
/// <param name="ProximoMantenimiento">Próximo preventivo.</param>
/// <param name="Vencido">Si la fecha ya pasó.</param>
/// <param name="Responsable">Quien lo tiene asignado, si alguien.</param>
/// <param name="SedeNombre">Red.</param>
public sealed record ProgramadoDto(
    Guid ActivoId,
    string CodigoPatrimonial,
    string TipoActivo,
    string Marca,
    string Modelo,
    string Establecimiento,
    DateOnly? UltimoPreventivo,
    DateOnly ProximoMantenimiento,
    bool Vencido,
    string? Responsable,
    string SedeNombre);

/// <summary>Las tres cifras de arriba de la página de mantenimiento.</summary>
/// <param name="EnCurso">Mantenimientos abiertos.</param>
/// <param name="Vencidos">Activos con el preventivo vencido (sin mantenimiento en curso, sin los de baja).</param>
/// <param name="Proximos">Activos con el preventivo en los próximos días (sin los de baja).</param>
/// <param name="Dias">Cuántos días cuentan como "próximos".</param>
public sealed record CifrasMantenimientoDto(int EnCurso, int Vencidos, int Proximos, int Dias);

/// <summary>Mantenimiento de un activo para su ficha.</summary>
/// <param name="EnCurso">El abierto, si hay.</param>
/// <param name="UltimoPreventivo">Término del último preventivo.</param>
/// <param name="ProximoMantenimiento">Próximo preventivo programado.</param>
/// <param name="Historial">Los cerrados, el más reciente primero.</param>
public sealed record MantenimientosActivoDto(
    MantenimientoDto? EnCurso,
    DateOnly? UltimoPreventivo,
    DateOnly? ProximoMantenimiento,
    IReadOnlyList<MantenimientoDto> Historial);

/// <summary>Un técnico que puede hacer mantenimientos (usuario de la red con permiso de mantenimiento).</summary>
public sealed record TecnicoDto(Guid Id, string NombreCompleto);

/// <summary>Cómo se cierra un mantenimiento y cómo queda el activo.</summary>
/// <param name="FechaFin">Término (no anterior al inicio ni posterior a hoy).</param>
/// <param name="TrabajoRealizado">Qué se hizo.</param>
/// <param name="Resultado">Operativo o sin reparación.</param>
/// <param name="Costo">Costo final (solo proveedor; nulo conserva el que tenía).</param>
/// <param name="EstadoActivoId">Estado en que queda el activo (la interfaz propone "Operativo" o "Inoperativo").</param>
/// <param name="Conservacion">Conservación en que queda.</param>
/// <param name="ProximoMantenimiento">Próximo preventivo; nulo = sin programar.</param>
public sealed record CierreMantenimiento(
    DateOnly FechaFin,
    string TrabajoRealizado,
    ResultadoMantenimiento Resultado,
    decimal? Costo,
    Guid EstadoActivoId,
    Conservacion Conservacion,
    DateOnly? ProximoMantenimiento);

/// <summary>Errores esperados del mantenimiento.</summary>
public static class ErroresMantenimiento
{
    public static readonly ErrorAplicacion NoEncontrado =
        ErrorAplicacion.NoEncontrado("Mantenimientos.NoEncontrado", "El mantenimiento no existe.");

    public static readonly ErrorAplicacion YaHayUnoAbierto =
        ErrorAplicacion.Conflicto(
            "Mantenimientos.YaHayUnoAbierto",
            "El activo ya tiene un mantenimiento en curso. Ciérrelo antes de registrar otro.");

    public static readonly ErrorAplicacion YaCerrado =
        ErrorAplicacion.ReglaDeNegocio("Mantenimientos.YaCerrado", "Este mantenimiento ya se cerró.");

    public static readonly ErrorAplicacion ActivoDeBaja =
        ErrorAplicacion.ReglaDeNegocio("Mantenimientos.ActivoDeBaja", "El activo está de baja: no se le registran mantenimientos.");

    public static readonly ErrorAplicacion TecnicoInvalido =
        ErrorAplicacion.Validacion("Mantenimientos.TecnicoInvalido", "El técnico no existe, está desactivado o es de otra red.");

    public static readonly ErrorAplicacion EstadoInvalido =
        ErrorAplicacion.Validacion("Mantenimientos.EstadoInvalido", "El estado elegido no existe o está desactivado.");

    public static readonly ErrorAplicacion FechaFutura =
        ErrorAplicacion.Validacion("Mantenimientos.FechaFutura", "La fecha no puede ser posterior a hoy.");
}

/// <summary>
/// Un mantenimiento con los datos del activo, como fila de consulta. Tiene propiedades (no constructor) para que EF Core
/// pueda filtrar y ordenar por ellas antes de traer los datos.
/// </summary>
internal sealed class FilaMantenimiento
{
    public Guid Id { get; init; }

    public Guid ActivoId { get; init; }

    public string CodigoPatrimonial { get; init; } = string.Empty;

    public string TipoActivo { get; init; } = string.Empty;

    public string Marca { get; init; } = string.Empty;

    public string Modelo { get; init; } = string.Empty;

    public Guid EstablecimientoId { get; init; }

    public string Establecimiento { get; init; } = string.Empty;

    public TipoMantenimiento Tipo { get; init; }

    public DateOnly FechaInicio { get; init; }

    public string? Detalle { get; init; }

    public EjecutorMantenimiento Ejecutor { get; init; }

    public Guid? TecnicoId { get; init; }

    public string? Tecnico { get; init; }

    public string? Proveedor { get; init; }

    public string? OrdenServicio { get; init; }

    public decimal? Costo { get; init; }

    public DateOnly? FechaFin { get; init; }

    public string? TrabajoRealizado { get; init; }

    public ResultadoMantenimiento? Resultado { get; init; }

    public DateTimeOffset CreadoEn { get; init; }

    public string SedeNombre { get; init; } = string.Empty;

    public MantenimientoDto ADto() =>
        new(Id, ActivoId, CodigoPatrimonial, TipoActivo, Marca, Modelo, Establecimiento, Tipo, FechaInicio, Detalle, Ejecutor,
            TecnicoId, Tecnico, Proveedor, OrdenServicio, Costo, FechaFin, TrabajoRealizado, Resultado, SedeNombre);
}

/// <summary>Consultas compartidas por las listas y la ficha.</summary>
internal static class ConsultasMantenimiento
{
    /// <summary>Filas con los datos del activo y el nombre del técnico; se ordenan y paginan antes de traerlas.</summary>
    public static IQueryable<FilaMantenimiento> Filas(IAppDbContext db, IQueryable<Mantenimiento> mantenimientos) =>
        from m in mantenimientos
        join a in db.Activos on m.ActivoId equals a.Id
        join t in db.TiposActivo on a.TipoActivoId equals t.Id
        join ma in db.Marcas on a.MarcaId equals ma.Id
        join e in db.Establecimientos on a.EstablecimientoId equals e.Id
        join s in db.Sedes on m.SedeId equals s.Id
        join u in db.Usuarios on m.TecnicoId equals (Guid?)u.Id into tecnicos
        from u in tecnicos.DefaultIfEmpty()
        select new FilaMantenimiento
        {
            Id = m.Id,
            ActivoId = a.Id,
            CodigoPatrimonial = a.CodigoPatrimonial,
            TipoActivo = t.Nombre,
            Marca = ma.Nombre,
            Modelo = a.Modelo,
            EstablecimientoId = e.Id,
            Establecimiento = e.Nombre,
            Tipo = m.Tipo,
            FechaInicio = m.FechaInicio,
            Detalle = m.Detalle,
            Ejecutor = m.Ejecutor,
            TecnicoId = m.TecnicoId,
            Tecnico = u != null ? u.Nombres + " " + u.Apellidos : null,
            Proveedor = m.Proveedor,
            OrdenServicio = m.OrdenServicio,
            Costo = m.Costo,
            FechaFin = m.FechaFin,
            TrabajoRealizado = m.TrabajoRealizado,
            Resultado = m.Resultado,
            CreadoEn = m.CreadoEn,
            SedeNombre = s.Nombre,
        };
}
