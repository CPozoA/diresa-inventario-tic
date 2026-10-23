using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Cuenta;

/// <summary>Sede del usuario tal como la ve la interfaz.</summary>
public sealed record SedeSesionDto(Guid Id, string Codigo, string Nombre);

/// <summary>
/// Lo que el frontend necesita saber del usuario conectado: quién es, de qué sede, si es superadministrador,
/// si debe cambiar su contraseña y qué permisos tiene (para mostrar u ocultar opciones; el servidor siempre vuelve a validar).
/// </summary>
public sealed record SesionDto(
    Guid UsuarioId,
    string NombreUsuario,
    string NombreCompleto,
    string? Correo,
    SedeSesionDto? Sede,
    bool EsSuperAdmin,
    bool DebeCambiarContrasena,
    IReadOnlyList<string> Permisos);

/// <summary>Arma el <see cref="SesionDto"/> de un usuario. Lo usan iniciar sesión y consultar la sesión.</summary>
/// <remarks>
/// Excepción revisada al filtro de sede: se consulta por el identificador que ya verificó la sesión o el inicio
/// de sesión. Durante el inicio de sesión la petición todavía es anónima y el filtro ocultaría al propio usuario.
/// </remarks>
internal sealed class ConstructorSesion(IAppDbContext db, IServicioIdentidad identidad, CalculadoraPermisos calculadora)
{
    public async Task<SesionDto?> ConstruirAsync(Guid usuarioId, CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

        var cuenta = await identidad.ObtenerDatosCuentaAsync(usuarioId);

        if (usuario is null || cuenta is null)
        {
            return null;
        }

        var permisos = (await calculadora.CalcularAsync(usuarioId, cancellationToken))
            .Order(StringComparer.Ordinal)
            .ToList();

        SedeSesionDto? sede = null;
        if (usuario.SedeId is Guid sedeId)
        {
            sede = await db.Sedes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(s => s.Id == sedeId)
                .Select(s => new SedeSesionDto(s.Id, s.Codigo, s.Nombre))
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new SesionDto(
            usuario.Id,
            cuenta.NombreUsuario,
            usuario.NombreCompleto,
            cuenta.Correo,
            sede,
            usuario.EsSuperAdmin,
            cuenta.DebeCambiarContrasena,
            permisos);
    }
}
