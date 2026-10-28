using Inventario.Application.Common.Reniec;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Personal;

namespace Inventario.Application.Personal;

/// <summary>Una persona en la lista del personal o en la búsqueda al asignar.</summary>
/// <param name="Id">Persona.</param>
/// <param name="Dni">DNI.</param>
/// <param name="NombreCompleto">Nombres y apellidos, en mayúsculas.</param>
/// <param name="Cargo">Cargo.</param>
/// <param name="EstablecimientoId">Establecimiento donde trabaja.</param>
/// <param name="Establecimiento">Nombre del establecimiento.</param>
/// <param name="AreaId">Área.</param>
/// <param name="Area">Nombre del área.</param>
/// <param name="Verificada">Falso: "Sin verificar en RENIEC".</param>
/// <param name="Activa">Falso si cesó.</param>
/// <param name="Equipos">Cuántos equipos tiene a su cargo hoy.</param>
/// <param name="SedeId">Red.</param>
/// <param name="SedeNombre">Nombre de la red (vista del superadministrador).</param>
public sealed record PersonaResumenDto(
    Guid Id,
    string Dni,
    string NombreCompleto,
    string? Cargo,
    Guid EstablecimientoId,
    string Establecimiento,
    Guid AreaId,
    string Area,
    bool Verificada,
    bool Activa,
    int Equipos,
    Guid SedeId,
    string SedeNombre);

/// <summary>Un equipo que la persona tiene o tuvo a su cargo.</summary>
/// <param name="AsignacionId">Asignación.</param>
/// <param name="ActivoId">Activo.</param>
/// <param name="CodigoPatrimonial">Código patrimonial.</param>
/// <param name="TipoActivo">Tipo de activo.</param>
/// <param name="Marca">Marca.</param>
/// <param name="Modelo">Modelo.</param>
/// <param name="FotoPrincipalId">Foto principal del activo, para la miniatura.</param>
/// <param name="FechaEntrega">Desde cuándo.</param>
/// <param name="ConservacionEntrega">Cómo lo recibió.</param>
/// <param name="FechaDevolucion">Hasta cuándo; nula si todavía lo tiene.</param>
/// <param name="ConservacionDevolucion">Cómo lo devolvió.</param>
public sealed record EquipoPersonaDto(
    Guid AsignacionId,
    Guid ActivoId,
    string CodigoPatrimonial,
    string TipoActivo,
    string Marca,
    string Modelo,
    Guid? FotoPrincipalId,
    DateOnly FechaEntrega,
    Conservacion ConservacionEntrega,
    DateOnly? FechaDevolucion,
    Conservacion? ConservacionDevolucion);

/// <summary>Ficha de una persona con su historial de equipos (los que tiene hoy primero).</summary>
/// <param name="Id">Persona.</param>
/// <param name="Dni">DNI.</param>
/// <param name="ApellidoPaterno">Apellido paterno.</param>
/// <param name="ApellidoMaterno">Apellido materno.</param>
/// <param name="Nombres">Nombres.</param>
/// <param name="NombreCompleto">Nombre completo.</param>
/// <param name="Cargo">Cargo.</param>
/// <param name="EstablecimientoId">Establecimiento.</param>
/// <param name="Establecimiento">Nombre del establecimiento.</param>
/// <param name="AreaId">Área.</param>
/// <param name="Area">Nombre del área.</param>
/// <param name="Verificada">Si los nombres se confirmaron con RENIEC.</param>
/// <param name="VerificadaEn">Cuándo se confirmaron.</param>
/// <param name="Activa">Falso si cesó.</param>
/// <param name="SedeId">Red.</param>
/// <param name="SedeNombre">Nombre de la red.</param>
/// <param name="Equipos">Asignaciones: vigentes primero y luego las más recientes.</param>
public sealed record PersonaDto(
    Guid Id,
    string Dni,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string Nombres,
    string NombreCompleto,
    string? Cargo,
    Guid EstablecimientoId,
    string Establecimiento,
    Guid AreaId,
    string Area,
    bool Verificada,
    DateTimeOffset? VerificadaEn,
    bool Activa,
    Guid SedeId,
    string SedeNombre,
    IReadOnlyList<EquipoPersonaDto> Equipos);

/// <summary>Persona que ya está registrada en la red con ese DNI.</summary>
public sealed record PersonaRegistradaDto(Guid Id, string NombreCompleto, bool Activa);

/// <summary>
/// Resultado de buscar un DNI antes de registrar a una persona. Si ya está registrada en la red no se consulta RENIEC.
/// </summary>
/// <param name="Dni">DNI consultado.</param>
/// <param name="Reniec">Qué respondió RENIEC; nulo si no se consultó porque la persona ya está registrada.</param>
/// <param name="ApellidoPaterno">De RENIEC, si se encontró.</param>
/// <param name="ApellidoMaterno">De RENIEC, si se encontró.</param>
/// <param name="Nombres">De RENIEC, si se encontró.</param>
/// <param name="Registrada">La persona con ese DNI que ya existe en la red, si la hay.</param>
public sealed record ConsultaDniDto(
    string Dni,
    EstadoConsultaDni? Reniec,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? Nombres,
    PersonaRegistradaDto? Registrada);

/// <summary>Errores esperados del personal.</summary>
public static class ErroresPersonal
{
    public static readonly ErrorAplicacion NoEncontrada =
        ErrorAplicacion.NoEncontrado("Personal.NoEncontrada", "La persona no existe.");

    public static readonly ErrorAplicacion DniInvalido =
        ErrorAplicacion.Validacion("Personal.DniInvalido", "El DNI debe tener 8 dígitos.");

    public static readonly ErrorAplicacion DniDuplicado =
        ErrorAplicacion.Conflicto("Personal.DniDuplicado", "Ya hay una persona con ese DNI en la red.");

    public static readonly ErrorAplicacion EstablecimientoInvalido =
        ErrorAplicacion.Validacion("Personal.EstablecimientoInvalido", "El establecimiento no existe o no pertenece a su red.");

    public static readonly ErrorAplicacion AreaInvalida =
        ErrorAplicacion.Validacion("Personal.AreaInvalida", "El área no existe, está desactivada o es de otra red.");

    public static readonly ErrorAplicacion OtraRed =
        ErrorAplicacion.ReglaDeNegocio(
            "Personal.OtraRed",
            "El establecimiento es de otra red. Si la persona se trasladó, regístrela en esa red.");

    public static readonly ErrorAplicacion DniNoEncontrado =
        ErrorAplicacion.ReglaDeNegocio("Personal.DniNoEncontrado", "RENIEC no encontró ese DNI. Revise los 8 dígitos.");

    public static readonly ErrorAplicacion ReniecNoDisponible =
        ErrorAplicacion.NoDisponible("Personal.ReniecNoDisponible", "No se pudo consultar RENIEC en este momento. Inténtelo en unos minutos.");

    public static ErrorAplicacion NombresObligatorios(EstadoConsultaDni estado) =>
        ErrorAplicacion.Validacion(
            "Personal.NombresObligatorios",
            estado == EstadoConsultaDni.NoEncontrado
                ? "RENIEC no encontró ese DNI: escriba el apellido paterno y los nombres."
                : "No se pudo consultar RENIEC: escriba el apellido paterno y los nombres.");

    public static ErrorAplicacion TieneEquipos(int cantidad) =>
        ErrorAplicacion.ReglaDeNegocio(
            "Personal.TieneEquipos",
            cantidad == 1
                ? "Tiene 1 equipo a su cargo. Devuélvalo o asígnelo a otra persona antes de marcarla como cesada."
                : $"Tiene {cantidad} equipos a su cargo. Devuélvalos o asígnelos a otra persona antes de marcarla como cesada.");
}

internal static class TextoDni
{
    /// <summary>El DNI sin espacios, o nulo si no son 8 dígitos.</summary>
    public static string? Limpiar(string? dni)
    {
        var limpio = dni?.Trim();
        return Persona.EsDniValido(limpio) ? limpio : null;
    }
}
