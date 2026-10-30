using Inventario.Domain.Common;

namespace Inventario.Domain.Auditoria;

/// <summary>Módulo al que pertenece una acción registrada en la auditoría.</summary>
public enum ModuloAuditoria
{
    Activos,
    Asignaciones,
    Personal,
    Mantenimiento,
    Licencias,
    Catalogos,
    Establecimientos,
    Usuarios,
    Roles,
    Sedes,

    /// <summary>Inicios de sesión, bloqueos y contraseñas.</summary>
    Cuenta,
}

/// <summary>
/// Una acción registrada en la auditoría (docs/ARQUITECTURA.md D56, §5.16): quién hizo qué, cuándo, en qué red y desde
/// qué dirección IP. Solo se agrega: no se modifica ni se borra desde el sistema. Guarda la acción en palabras
/// ("Editó el activo PAT-0001"), sin los valores anteriores y nuevos.
/// </summary>
public sealed class RegistroAuditoria : Entidad
{
    public const int LongitudMaximaAccion = 500;
    public const int LongitudMaximaIp = 45;

    // Requerido por EF Core.
    private RegistroAuditoria()
    {
    }

    public DateTimeOffset Fecha { get; private set; }

    /// <summary>Quién lo hizo; nulo en un inicio de sesión fallido con un usuario que no existe.</summary>
    public Guid? UsuarioId { get; private set; }

    /// <summary>Red del registro afectado; nula en lo que no es de una red (catálogos generales, sedes).</summary>
    public Guid? SedeId { get; private set; }

    public ModuloAuditoria Modulo { get; private set; }

    public string Accion { get; private set; } = string.Empty;

    /// <summary>Activo afectado, para su historial (sin clave foránea: el registro sobrevive al activo).</summary>
    public Guid? ActivoId { get; private set; }

    public string? Ip { get; private set; }

    public static RegistroAuditoria Crear(
        DateTimeOffset fecha, Guid? usuarioId, Guid? sedeId, ModuloAuditoria modulo, string accion, Guid? activoId, string? ip)
    {
        var texto = accion?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            throw new DominioException("Describa la acción.");
        }

        return new RegistroAuditoria
        {
            Fecha = fecha,
            UsuarioId = usuarioId,
            SedeId = sedeId,
            Modulo = modulo,
            Accion = texto.Length > LongitudMaximaAccion ? texto[..(LongitudMaximaAccion - 1)] + "…" : texto,
            ActivoId = activoId,
            Ip = ip is { Length: > LongitudMaximaIp } ? ip[..LongitudMaximaIp] : ip,
        };
    }
}
