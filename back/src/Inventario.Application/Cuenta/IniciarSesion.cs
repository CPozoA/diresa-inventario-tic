using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Cuenta;

/// <summary>Iniciar sesión con nombre de usuario (o correo) y contraseña.</summary>
public sealed record IniciarSesionCommand(string Usuario, string Contrasena) : ICommand<SesionDto>;

internal sealed class IniciarSesionHandler(
    IServicioIdentidad identidad,
    IAppDbContext db,
    ConstructorSesion constructorSesion,
    TimeProvider reloj,
    IAuditoria auditoria) : ICommandHandler<IniciarSesionCommand, SesionDto>
{
    public async Task<Result<SesionDto>> Handle(IniciarSesionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Usuario) || string.IsNullOrEmpty(command.Contrasena))
        {
            return ErroresCuenta.DatosIncompletos;
        }

        // 1. Credenciales: cuenta los intentos fallidos y bloquea al tercero.
        var verificacion = await identidad.VerificarCredencialesAsync(command.Usuario.Trim(), command.Contrasena);

        if (verificacion.Resultado == ResultadoVerificacion.Bloqueada)
        {
            await AuditarAsync(
                verificacion.UsuarioId, "Intento de inicio de sesión con la cuenta bloqueada (3 intentos fallidos seguidos)", cancellationToken);
            return ErroresCuenta.Bloqueada(MinutosRestantes(verificacion.BloqueadaHasta));
        }

        if (verificacion.Resultado != ResultadoVerificacion.Correcta || verificacion.UsuarioId is not Guid usuarioId)
        {
            await AuditarAsync(
                verificacion.UsuarioId,
                verificacion.UsuarioId is null
                    ? $"Inicio de sesión fallido con el usuario «{command.Usuario.Trim()}», que no existe"
                    : "Inicio de sesión fallido: contraseña incorrecta",
                cancellationToken);
            return ErroresCuenta.CredencialesInvalidas;
        }

        // 2. Estado: usuario activo y, si pertenece a una sede, sede activa.
        // Excepción revisada al filtro de sede: la petición aún es anónima y el usuario ya fue verificado por sus credenciales.
        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => new { u.Activo, u.SedeId })
            .FirstOrDefaultAsync(cancellationToken);

        if (usuario is null || !usuario.Activo)
        {
            await AuditarAsync(usuarioId, "Intento de inicio de sesión con la cuenta desactivada", cancellationToken);
            return ErroresCuenta.Inactiva;
        }

        if (usuario.SedeId is Guid sedeId
            && !await db.Sedes.IgnoreQueryFilters().AnyAsync(s => s.Id == sedeId && s.Activa, cancellationToken))
        {
            await AuditarAsync(usuarioId, "Intento de inicio de sesión en una sede desactivada", cancellationToken);
            return ErroresCuenta.SedeInactiva;
        }

        // 3. Sesión.
        await identidad.IniciarSesionAsync(usuarioId);
        await AuditarAsync(usuarioId, "Inició sesión", cancellationToken);

        var sesion = await constructorSesion.ConstruirAsync(usuarioId, cancellationToken);
        return sesion is null
            ? ErroresCuenta.CredencialesInvalidas
            : Result.Exito(sesion);
    }

    /// <summary>Registra el intento (D56) con la red del usuario, si existe; la petición todavía es anónima.</summary>
    private async Task AuditarAsync(Guid? usuarioId, string accion, CancellationToken cancellationToken)
    {
        Guid? sedeId = null;
        if (usuarioId is Guid id)
        {
            // Excepción revisada al filtro de sede: solo se lee la red del usuario para ubicar el registro.
            sedeId = await db.Usuarios.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => u.SedeId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        await auditoria.RegistrarYGuardarAsync(ModuloAuditoria.Cuenta, accion, sedeId, usuarioId: usuarioId, cancellationToken: cancellationToken);
    }

    private int MinutosRestantes(DateTimeOffset? hasta)
    {
        if (hasta is not DateTimeOffset fin)
        {
            return 1;
        }

        var minutos = (int)Math.Ceiling((fin - reloj.GetUtcNow()).TotalMinutes);
        return Math.Max(minutos, 1);
    }
}
