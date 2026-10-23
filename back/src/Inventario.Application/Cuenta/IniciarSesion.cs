using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Cuenta;

/// <summary>Iniciar sesión con nombre de usuario (o correo) y contraseña.</summary>
public sealed record IniciarSesionCommand(string Usuario, string Contrasena) : ICommand<SesionDto>;

internal sealed class IniciarSesionHandler(
    IServicioIdentidad identidad,
    IAppDbContext db,
    ConstructorSesion constructorSesion,
    TimeProvider reloj) : ICommandHandler<IniciarSesionCommand, SesionDto>
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
            return ErroresCuenta.Bloqueada(MinutosRestantes(verificacion.BloqueadaHasta));
        }

        if (verificacion.Resultado != ResultadoVerificacion.Correcta || verificacion.UsuarioId is not Guid usuarioId)
        {
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
            return ErroresCuenta.Inactiva;
        }

        if (usuario.SedeId is Guid sedeId
            && !await db.Sedes.IgnoreQueryFilters().AnyAsync(s => s.Id == sedeId && s.Activa, cancellationToken))
        {
            return ErroresCuenta.SedeInactiva;
        }

        // 3. Sesión.
        await identidad.IniciarSesionAsync(usuarioId);

        var sesion = await constructorSesion.ConstruirAsync(usuarioId, cancellationToken);
        return sesion is null
            ? ErroresCuenta.CredencialesInvalidas
            : Result.Exito(sesion);
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
