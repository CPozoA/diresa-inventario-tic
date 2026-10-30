using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;

namespace Inventario.Application.Auditoria;

/// <summary>
/// Registro de auditoría (D56). Cada caso de uso que cambia datos llama a <see cref="Registrar"/> antes de guardar: la
/// línea se guarda en la misma operación que el cambio, así nunca hay un cambio sin su registro ni un registro de algo
/// que no se guardó.
/// </summary>
public interface IAuditoria
{
    /// <summary>Agrega la acción; se guarda con el próximo <c>SaveChangesAsync</c> del caso de uso.</summary>
    /// <param name="modulo">Módulo.</param>
    /// <param name="accion">En palabras y en pasado: "Registró el activo PAT-0001".</param>
    /// <param name="sedeId">Red del registro afectado (nula si no es de una red).</param>
    /// <param name="activoId">Activo afectado, para su historial.</param>
    /// <param name="usuarioId">Quién; por omisión, el usuario de la sesión.</param>
    void Registrar(ModuloAuditoria modulo, string accion, Guid? sedeId, Guid? activoId = null, Guid? usuarioId = null);

    /// <summary>
    /// Agrega la acción y la guarda de inmediato, para los casos de uso que no guardan con el contexto (operaciones de
    /// Identity: crear cuentas, contraseñas, inicios de sesión).
    /// </summary>
    Task RegistrarYGuardarAsync(
        ModuloAuditoria modulo, string accion, Guid? sedeId, Guid? activoId = null, Guid? usuarioId = null, CancellationToken cancellationToken = default);
}

internal sealed class ServicioAuditoria(IAppDbContext db, IUsuarioActual usuario, IContextoPeticion peticion, TimeProvider reloj) : IAuditoria
{
    public void Registrar(ModuloAuditoria modulo, string accion, Guid? sedeId, Guid? activoId = null, Guid? usuarioId = null) =>
        db.Auditoria.Add(RegistroAuditoria.Crear(reloj.GetUtcNow(), usuarioId ?? usuario.UsuarioId, sedeId, modulo, accion, activoId, peticion.Ip));

    public async Task RegistrarYGuardarAsync(
        ModuloAuditoria modulo, string accion, Guid? sedeId, Guid? activoId = null, Guid? usuarioId = null, CancellationToken cancellationToken = default)
    {
        Registrar(modulo, accion, sedeId, activoId, usuarioId);
        await db.SaveChangesAsync(cancellationToken);
    }
}
