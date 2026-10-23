using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Usuarios.Obtener;

/// <summary>Un usuario de la sede. Un usuario de otra sede (o el superadministrador) responde "no existe".</summary>
/// <remarks>Requiere Usuarios.Ver.</remarks>
public sealed record ObtenerUsuarioQuery(Guid Id) : IQuery<UsuarioDto>;

internal sealed class ObtenerUsuarioHandler(IAppDbContext db, IServicioIdentidad identidad)
    : IQueryHandler<ObtenerUsuarioQuery, UsuarioDto>
{
    public async Task<Result<UsuarioDto>> Handle(ObtenerUsuarioQuery query, CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == query.Id && u.SedeId != null, cancellationToken);

        if (usuario is null)
        {
            return ErroresUsuario.NoEncontrado;
        }

        var cuenta = await identidad.ObtenerDatosCuentaAsync(usuario.Id);

        var rolIds = usuario.Roles.Select(r => r.RolId).ToList();
        var roles = await db.Roles
            .AsNoTracking()
            .Where(r => rolIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return MapeoUsuario.ADto(usuario, cuenta, roles);
    }
}
