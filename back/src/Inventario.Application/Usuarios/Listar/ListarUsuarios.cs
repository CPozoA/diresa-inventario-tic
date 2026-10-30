using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Usuarios.Listar;

/// <summary>Usuarios de una sede, ordenados por apellidos. Nunca incluye al superadministrador.</summary>
/// <param name="SedeId">Para el superadministrador es opcional (sin sede: todos los usuarios de todas las sedes).</param>
/// <param name="Buscar">Texto opcional: busca en nombres, apellidos y cargo; si son solo dígitos, el DNI desde el inicio.</param>
/// <remarks>Requiere Usuarios.Ver.</remarks>
public sealed record ListarUsuariosQuery(Guid? SedeId, string? Buscar) : IQuery<IReadOnlyList<UsuarioDto>>;

internal sealed class ListarUsuariosHandler(
    IAppDbContext db,
    IUsuarioActual actual,
    ReglasDeGestion reglas,
    IServicioIdentidad identidad) : IQueryHandler<ListarUsuariosQuery, IReadOnlyList<UsuarioDto>>
{
    public async Task<Result<IReadOnlyList<UsuarioDto>>> Handle(ListarUsuariosQuery query, CancellationToken cancellationToken)
    {
        var consulta = db.Usuarios
            .AsNoTracking()
            .Include(u => u.Roles)
            .Where(u => u.SedeId != null);

        if (!actual.EsSuperAdmin || query.SedeId is not null)
        {
            var sede = await reglas.ResolverSedeAsync(query.SedeId, cancellationToken);
            if (!sede.EsExito)
            {
                return Result.Fallo<IReadOnlyList<UsuarioDto>>(sede.Error!);
            }

            var sedeId = sede.Valor;
            consulta = consulta.Where(u => u.SedeId == sedeId);
        }

        if (!string.IsNullOrWhiteSpace(query.Buscar))
        {
            var texto = query.Buscar.Trim();
            consulta = texto.All(char.IsAsciiDigit)
                ? consulta.Where(u => u.Dni != null && u.Dni.StartsWith(texto))
                : consulta.Where(u =>
                    u.Nombres.Contains(texto) || u.Apellidos.Contains(texto) || (u.Cargo != null && u.Cargo.Contains(texto)));
        }

        var usuarios = await consulta
            .OrderBy(u => u.Apellidos)
            .ThenBy(u => u.Nombres)
            .ToListAsync(cancellationToken);

        var cuentas = await identidad.ObtenerDatosCuentasAsync([.. usuarios.Select(u => u.Id)], cancellationToken);

        var rolIds = usuarios.SelectMany(u => u.Roles).Select(r => r.RolId).Distinct().ToList();
        var roles = await db.Roles
            .AsNoTracking()
            .Where(r => rolIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var lista = usuarios
            .Select(u => MapeoUsuario.ADto(u, cuentas.GetValueOrDefault(u.Id), roles))
            .ToList();

        return Result.Exito<IReadOnlyList<UsuarioDto>>(lista);
    }
}
