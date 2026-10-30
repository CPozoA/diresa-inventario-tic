using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Reniec;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Personal;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Usuarios.ConsultarDni;

/// <summary>
/// Busca un DNI antes de crear un usuario (o al completarlo en uno anterior): si ya es de otro usuario lo avisa sin
/// consultar RENIEC; si no, devuelve lo que responde RENIEC para llenar nombres y apellidos. No guarda nada.
/// </summary>
/// <param name="Dni">8 dígitos.</param>
/// <param name="ExcluirUsuarioId">Al editar: el usuario que se edita (su propio DNI no cuenta como repetido).</param>
/// <remarks>Requiere Usuarios.Gestionar (cada consulta a RENIEC tiene costo).</remarks>
public sealed record ConsultarDniUsuarioQuery(string? Dni, Guid? ExcluirUsuarioId) : IQuery<ConsultaDniUsuarioDto>;

/// <summary>Usuario que ya tiene el DNI consultado, de cualquier red.</summary>
/// <param name="NombreCompleto">Nombres y apellidos.</param>
/// <param name="Sede">Red a la que pertenece.</param>
public sealed record UsuarioConDniDto(string NombreCompleto, string? Sede);

/// <summary>Resultado de buscar un DNI para un usuario.</summary>
/// <param name="Dni">DNI consultado.</param>
/// <param name="Reniec">Qué respondió RENIEC; nulo si no se consultó porque el DNI ya es de otro usuario.</param>
/// <param name="Nombres">De RENIEC, si se encontró (en mayúsculas, como los da RENIEC).</param>
/// <param name="Apellidos">Apellido paterno y materno de RENIEC, si se encontró.</param>
/// <param name="Registrado">El usuario que ya tiene ese DNI, si lo hay.</param>
public sealed record ConsultaDniUsuarioDto(
    string Dni,
    EstadoConsultaDni? Reniec,
    string? Nombres,
    string? Apellidos,
    UsuarioConDniDto? Registrado);

internal sealed class ConsultarDniUsuarioHandler(IAppDbContext db, IConsultaDni reniec)
    : IQueryHandler<ConsultarDniUsuarioQuery, ConsultaDniUsuarioDto>
{
    public async Task<Result<ConsultaDniUsuarioDto>> Handle(ConsultarDniUsuarioQuery query, CancellationToken cancellationToken)
    {
        if (TextoDni.Limpiar(query.Dni) is not string dni)
        {
            return ErroresUsuario.DniInvalido;
        }

        if (await db.BuscarPorDniAsync(dni, query.ExcluirUsuarioId, cancellationToken) is { } existente)
        {
            var sede = existente.SedeId is Guid sedeId
                ? await db.Sedes.AsNoTracking().Where(s => s.Id == sedeId).Select(s => s.Nombre).FirstOrDefaultAsync(cancellationToken)
                : null;

            return new ConsultaDniUsuarioDto(dni, null, null, null, new UsuarioConDniDto(existente.NombreCompleto, sede));
        }

        var resultado = await reniec.ConsultarAsync(dni, cancellationToken);
        var n = resultado.Nombres;
        var apellidos = n is null ? null : string.Join(' ', new[] { n.ApellidoPaterno, n.ApellidoMaterno }.Where(a => !string.IsNullOrWhiteSpace(a)));

        return new ConsultaDniUsuarioDto(dni, resultado.Estado, n?.Nombres, apellidos, null);
    }
}
