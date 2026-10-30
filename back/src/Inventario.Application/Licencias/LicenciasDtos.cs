using Inventario.Application.Common.Resultados;
using Inventario.Domain.Catalogos;

namespace Inventario.Application.Licencias;

/// <summary>Situación de una licencia según su vencimiento (D54).</summary>
public enum VigenciaLicencia
{
    /// <summary>Rige y faltan más de 30 días para el vencimiento.</summary>
    Vigente,

    /// <summary>Vence dentro de los próximos 30 días (el día del vencimiento incluido).</summary>
    PorVencer,

    /// <summary>Ya pasó la fecha de vencimiento.</summary>
    Vencida,

    /// <summary>No se renovó; no cuenta en las alertas.</summary>
    Archivada,
}

/// <summary>Filtro de la lista de licencias.</summary>
public enum FiltroLicencias
{
    /// <summary>Las que rigen (también las por vencer).</summary>
    Vigentes,

    PorVencer,

    Vencidas,

    /// <summary>Con más equipos que puestos.</summary>
    Excedidas,

    Archivadas,
}

/// <summary>Una licencia en la lista.</summary>
/// <param name="Id">Licencia.</param>
/// <param name="Producto">Producto.</param>
/// <param name="Proveedor">Proveedor.</param>
/// <param name="Puestos">Puestos comprados.</param>
/// <param name="Equipos">Equipos asignados.</param>
/// <param name="FechaInicio">Inicio.</param>
/// <param name="FechaFin">Vencimiento (último día incluido).</param>
/// <param name="DocumentoCompra">Orden de compra, contrato o factura.</param>
/// <param name="Vigencia">Vigente, por vencer, vencida o archivada.</param>
/// <param name="DiasRestantes">Días hasta el vencimiento (negativo si ya venció).</param>
/// <param name="Excedida">Más equipos que puestos.</param>
/// <param name="SedeNombre">Red (vista del superadministrador).</param>
public sealed record LicenciaDto(
    Guid Id,
    string Producto,
    string? Proveedor,
    int Puestos,
    int Equipos,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? DocumentoCompra,
    VigenciaLicencia Vigencia,
    int DiasRestantes,
    bool Excedida,
    string SedeNombre);

/// <summary>Cifras de la página de licencias y del resumen. No cuentan las archivadas.</summary>
/// <param name="Vigentes">Rigen hoy (también las por vencer).</param>
/// <param name="PorVencer">Vencen en los próximos <paramref name="Dias"/> días.</param>
/// <param name="Vencidas">Ya vencieron y no se archivaron.</param>
/// <param name="Excedidas">Con más equipos que puestos.</param>
/// <param name="PuestosLibres">Puestos sin equipo en las vigentes.</param>
/// <param name="Dias">Días de aviso antes del vencimiento.</param>
public sealed record CifrasLicenciasDto(int Vigentes, int PorVencer, int Vencidas, int Excedidas, int PuestosLibres, int Dias);

/// <summary>Un equipo que ocupa un puesto de la licencia.</summary>
/// <param name="ActivoId">Activo.</param>
/// <param name="CodigoPatrimonial">Código patrimonial.</param>
/// <param name="TipoActivo">Tipo de activo.</param>
/// <param name="Marca">Marca.</param>
/// <param name="Modelo">Modelo.</param>
/// <param name="NombreEquipo">Nombre del equipo en la red.</param>
/// <param name="Establecimiento">Establecimiento.</param>
/// <param name="Responsable">Quién lo tiene asignado.</param>
/// <param name="Estado">Estado del activo.</param>
/// <param name="GrupoEstado">Grupo del estado (los de baja se resaltan para quitarlos).</param>
/// <param name="AsignadoEn">Desde cuándo ocupa el puesto.</param>
public sealed record EquipoLicenciaDto(
    Guid ActivoId,
    string CodigoPatrimonial,
    string TipoActivo,
    string Marca,
    string Modelo,
    string? NombreEquipo,
    string Establecimiento,
    string? Responsable,
    string Estado,
    GrupoEstado GrupoEstado,
    DateOnly AsignadoEn);

/// <summary>Ficha de una licencia con sus equipos.</summary>
/// <param name="Id">Licencia.</param>
/// <param name="SedeId">Red.</param>
/// <param name="SedeNombre">Nombre de la red.</param>
/// <param name="Producto">Producto.</param>
/// <param name="Proveedor">Proveedor.</param>
/// <param name="Puestos">Puestos comprados.</param>
/// <param name="FechaInicio">Inicio.</param>
/// <param name="FechaFin">Vencimiento.</param>
/// <param name="DocumentoCompra">Orden de compra, contrato o factura.</param>
/// <param name="Costo">Costo total en soles.</param>
/// <param name="Observaciones">Notas.</param>
/// <param name="Vigencia">Vigente, por vencer, vencida o archivada.</param>
/// <param name="DiasRestantes">Días hasta el vencimiento (negativo si ya venció).</param>
/// <param name="Equipos">Equipos asignados, el más reciente primero.</param>
/// <param name="CreadoEn">Registro.</param>
/// <param name="ModificadoEn">Última modificación.</param>
public sealed record FichaLicenciaDto(
    Guid Id,
    Guid SedeId,
    string SedeNombre,
    string Producto,
    string? Proveedor,
    int Puestos,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? DocumentoCompra,
    decimal? Costo,
    string? Observaciones,
    VigenciaLicencia Vigencia,
    int DiasRestantes,
    IReadOnlyList<EquipoLicenciaDto> Equipos,
    DateTimeOffset CreadoEn,
    DateTimeOffset? ModificadoEn);

/// <summary>Una licencia instalada en el activo (sección de su ficha).</summary>
/// <param name="LicenciaId">Licencia.</param>
/// <param name="Producto">Producto.</param>
/// <param name="FechaFin">Vencimiento.</param>
/// <param name="Vigencia">Vigente, por vencer o vencida.</param>
/// <param name="DiasRestantes">Días hasta el vencimiento.</param>
/// <param name="AsignadoEn">Desde cuándo.</param>
public sealed record LicenciaDelActivoDto(
    Guid LicenciaId, string Producto, DateOnly FechaFin, VigenciaLicencia Vigencia, int DiasRestantes, DateOnly AsignadoEn);

/// <summary>Errores de licencias.</summary>
public static class ErroresLicencia
{
    public static readonly ErrorAplicacion NoEncontrada =
        ErrorAplicacion.NoEncontrado("Licencias.NoEncontrada", "La licencia no existe.");

    public static readonly ErrorAplicacion Archivada =
        ErrorAplicacion.ReglaDeNegocio("Licencias.Archivada", "La licencia está archivada. Reactívela para asignar equipos.");

    public static readonly ErrorAplicacion Vencida =
        ErrorAplicacion.ReglaDeNegocio("Licencias.Vencida", "La licencia venció. Renuévela (nuevas fechas) antes de asignar equipos.");

    public static readonly ErrorAplicacion SinEquipos =
        ErrorAplicacion.Validacion("Licencias.SinEquipos", "Elija al menos un equipo.");

    public static readonly ErrorAplicacion EquipoNoEncontrado =
        ErrorAplicacion.NoEncontrado("Licencias.EquipoNoEncontrado", "Uno de los equipos no existe o es de otra red.");

    public static readonly ErrorAplicacion EquipoDeBaja =
        ErrorAplicacion.ReglaDeNegocio("Licencias.EquipoDeBaja", "Uno de los equipos está de baja: no se le asignan licencias.");

    public static readonly ErrorAplicacion YaAsignada =
        ErrorAplicacion.Conflicto("Licencias.YaAsignada", "Uno de los equipos ya tiene esta licencia.");

    public static readonly ErrorAplicacion NoAsignada =
        ErrorAplicacion.NoEncontrado("Licencias.NoAsignada", "El equipo no tiene esta licencia.");

    public static ErrorAplicacion SinPuestos(int libres) =>
        ErrorAplicacion.ReglaDeNegocio(
            "Licencias.SinPuestos",
            libres == 0
                ? "La licencia no tiene puestos libres. Quite equipos o aumente los puestos."
                : $"La licencia solo tiene {libres} {(libres == 1 ? "puesto libre" : "puestos libres")}.");
}

/// <summary>Cálculo de la vigencia, igual en todas las consultas.</summary>
public static class ReglasVigencia
{
    /// <summary>Días antes del vencimiento en que la licencia pasa a "por vencer".</summary>
    public const int DiasAviso = 30;

    public static VigenciaLicencia Calcular(DateOnly fechaFin, bool archivada, DateOnly hoy) =>
        archivada ? VigenciaLicencia.Archivada
        : fechaFin < hoy ? VigenciaLicencia.Vencida
        : fechaFin <= hoy.AddDays(DiasAviso) ? VigenciaLicencia.PorVencer
        : VigenciaLicencia.Vigente;

    public static int DiasRestantes(DateOnly fechaFin, DateOnly hoy) => fechaFin.DayNumber - hoy.DayNumber;
}
