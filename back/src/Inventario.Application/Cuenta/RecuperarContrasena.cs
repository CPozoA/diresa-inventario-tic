using System.Net;
using Inventario.Application.Common.Correo;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Cuenta;

/// <summary>
/// "Olvidé mi contraseña": envía un enlace de un solo uso al correo de la cuenta.
/// Responde siempre lo mismo, exista o no el correo, para no revelar qué cuentas existen.
/// </summary>
public sealed record SolicitarRestablecimientoCommand(string Correo) : ICommand<Vacio>;

/// <summary>Define una contraseña nueva usando el enlace recibido por correo.</summary>
public sealed record RestablecerContrasenaCommand(Guid UsuarioId, string Token, string ContrasenaNueva) : ICommand<Vacio>;

internal sealed class SolicitarRestablecimientoHandler(
    IServicioIdentidad identidad,
    IAppDbContext db,
    IEnlacesAplicacion enlaces,
    IServicioCorreo correo) : ICommandHandler<SolicitarRestablecimientoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(SolicitarRestablecimientoCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Correo))
        {
            return ErroresCuenta.DatosIncompletos;
        }

        var solicitud = await identidad.GenerarTokenRestablecimientoAsync(command.Correo.Trim());
        if (solicitud is null)
        {
            return Result.Exito(Vacio.Valor);
        }

        // Excepción revisada al filtro de sede: petición anónima; el usuario sale del correo registrado en su cuenta.
        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == solicitud.UsuarioId)
            .Select(u => new { u.Activo, u.Nombres })
            .FirstOrDefaultAsync(cancellationToken);

        // A una cuenta desactivada no se le envía enlace (la respuesta sigue siendo la misma).
        if (usuario is null || !usuario.Activo)
        {
            return Result.Exito(Vacio.Valor);
        }

        var enlace = enlaces.RestablecerContrasena(solicitud.UsuarioId, solicitud.Token);
        await correo.EnviarAsync(PlantillasCorreo.RestablecerContrasena(solicitud.Correo, usuario.Nombres, enlace), cancellationToken);

        return Result.Exito(Vacio.Valor);
    }
}

internal sealed class RestablecerContrasenaHandler(IServicioIdentidad identidad)
    : ICommandHandler<RestablecerContrasenaCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(RestablecerContrasenaCommand command, CancellationToken cancellationToken)
    {
        if (command.UsuarioId == Guid.Empty || string.IsNullOrEmpty(command.Token) || string.IsNullOrEmpty(command.ContrasenaNueva))
        {
            return ErroresCuenta.DatosIncompletos;
        }

        var resultado = await identidad.RestablecerContrasenaAsync(command.UsuarioId, command.Token, command.ContrasenaNueva);
        return resultado.EsExito
            ? Result.Exito(Vacio.Valor)
            : Result.Fallo<Vacio>(resultado.Error!);
    }
}

internal static class PlantillasCorreo
{
    public static MensajeCorreo RestablecerContrasena(string para, string nombres, Uri enlace)
    {
        var nombre = WebUtility.HtmlEncode(nombres);
        var url = WebUtility.HtmlEncode(enlace.ToString());

        var cuerpo = $"""
            <p>Hola, {nombre}:</p>
            <p>Recibimos una solicitud para restablecer su contraseña del <strong>Sistema de Inventario TIC</strong>.</p>
            <p><a href="{url}">Restablecer mi contraseña</a></p>
            <p>El enlace vence en 1 hora y solo se puede usar una vez.</p>
            <p>Si usted no lo solicitó, ignore este correo: su contraseña no cambiará.</p>
            """;

        return new MensajeCorreo(para, "Restablecer contraseña - Inventario TIC", cuerpo);
    }
}
