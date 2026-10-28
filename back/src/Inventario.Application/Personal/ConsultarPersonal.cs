using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Reniec;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Personal;

/// <summary>
/// Busca un DNI antes de registrar a una persona: primero en el personal de la red y, si no está, en RENIEC.
/// </summary>
/// <param name="Dni">8 dígitos.</param>
/// <param name="SedeId">Solo para el superadministrador: la red donde se registrará.</param>
/// <remarks>Requiere Activos.Editar (cada consulta a RENIEC tiene costo).</remarks>
public sealed record ConsultarDniQuery(string? Dni, Guid? SedeId) : IQuery<ConsultaDniDto>;

/// <summary>Personal de la red ordenado por apellidos, con cuántos equipos tiene cada uno.</summary>
/// <param name="SedeId">Solo para el superadministrador: limita a una red.</param>
/// <param name="Buscar">DNI (desde el inicio) o palabras del nombre completo, sin importar tildes ni el orden.</param>
/// <param name="EstablecimientoId">Filtra por establecimiento.</param>
/// <param name="IncluirCesados">Falso (por omisión): solo el personal activo, como al asignar.</param>
/// <param name="Maximo">Entre 1 y 1000 (por omisión, 1000); la búsqueda al asignar pide pocos.</param>
/// <remarks>Requiere Activos.Ver.</remarks>
public sealed record ListarPersonalQuery(Guid? SedeId, string? Buscar, Guid? EstablecimientoId, bool IncluirCesados, int? Maximo)
    : IQuery<IReadOnlyList<PersonaResumenDto>>;

/// <summary>Ficha de una persona con los equipos que tiene y tuvo. Una persona de otra red "no existe".</summary>
/// <remarks>Requiere Activos.Ver.</remarks>
public sealed record ObtenerPersonaQuery(Guid Id) : IQuery<PersonaDto>;

internal sealed class ConsultarDniHandler(IAppDbContext db, IUsuarioActual usuario, IConsultaDni reniec)
    : IQueryHandler<ConsultarDniQuery, ConsultaDniDto>
{
    public async Task<Result<ConsultaDniDto>> Handle(ConsultarDniQuery query, CancellationToken cancellationToken)
    {
        if (TextoDni.Limpiar(query.Dni) is not string dni)
        {
            return ErroresPersonal.DniInvalido;
        }

        // Un usuario de sede busca en su red; el superadministrador, en la red que eligió (si eligió una).
        if ((usuario.SedeId ?? query.SedeId) is Guid sedeId)
        {
            var registrada = await db.Personal.AsNoTracking()
                .Where(p => p.SedeId == sedeId && p.Dni == dni)
                .Select(p => new PersonaRegistradaDto(p.Id, p.NombreCompleto, p.Activa))
                .FirstOrDefaultAsync(cancellationToken);

            if (registrada is not null)
            {
                return new ConsultaDniDto(dni, null, null, null, null, registrada);
            }
        }

        var resultado = await reniec.ConsultarAsync(dni, cancellationToken);
        var n = resultado.Nombres;
        return new ConsultaDniDto(dni, resultado.Estado, n?.ApellidoPaterno, n?.ApellidoMaterno, n?.Nombres, null);
    }
}

internal sealed class ListarPersonalHandler(IAppDbContext db) : IQueryHandler<ListarPersonalQuery, IReadOnlyList<PersonaResumenDto>>
{
    public const int MaximoPermitido = 1000;

    public async Task<Result<IReadOnlyList<PersonaResumenDto>>> Handle(ListarPersonalQuery query, CancellationToken cancellationToken)
    {
        var maximo = Math.Clamp(query.Maximo ?? MaximoPermitido, 1, MaximoPermitido);

        // El filtro global de sede ya limita a un usuario de sede a la suya.
        var personas = db.Personal.AsNoTracking();

        if (query.SedeId is Guid sedeId)
        {
            personas = personas.Where(p => p.SedeId == sedeId);
        }

        if (query.EstablecimientoId is Guid establecimientoId)
        {
            personas = personas.Where(p => p.EstablecimientoId == establecimientoId);
        }

        if (!query.IncluirCesados)
        {
            personas = personas.Where(p => p.Activa);
        }

        var buscar = query.Buscar?.Trim();
        if (!string.IsNullOrEmpty(buscar))
        {
            if (buscar.All(char.IsAsciiDigit))
            {
                personas = personas.Where(p => p.Dni.StartsWith(buscar));
            }
            else
            {
                // Cada palabra debe aparecer en el nombre completo ("quispe juan" encuentra a "JUAN PÉREZ QUISPE").
                foreach (var palabra in buscar.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    personas = personas.Where(p => p.NombreCompleto.Contains(palabra));
                }
            }
        }

        var filas = await (
            from p in personas
            join e in db.Establecimientos on p.EstablecimientoId equals e.Id
            join ar in db.Areas on p.AreaId equals ar.Id
            join s in db.Sedes on p.SedeId equals s.Id
            orderby p.ApellidoPaterno, p.ApellidoMaterno, p.Nombres
            select new PersonaResumenDto(
                p.Id,
                p.Dni,
                p.NombreCompleto,
                p.Cargo,
                e.Id,
                e.Nombre,
                ar.Id,
                ar.Nombre,
                p.VerificadaEn != null,
                p.Activa,
                db.Asignaciones.Count(x => x.PersonaId == p.Id && x.FechaDevolucion == null),
                s.Id,
                s.Nombre))
            .Take(maximo)
            .ToListAsync(cancellationToken);

        return filas;
    }
}

internal sealed class ObtenerPersonaHandler(IAppDbContext db) : IQueryHandler<ObtenerPersonaQuery, PersonaDto>
{
    public async Task<Result<PersonaDto>> Handle(ObtenerPersonaQuery query, CancellationToken cancellationToken)
    {
        var persona = await db.Personal.AsNoTracking().FirstOrDefaultAsync(p => p.Id == query.Id, cancellationToken);
        if (persona is null)
        {
            return ErroresPersonal.NoEncontrada;
        }

        var establecimiento = await db.Establecimientos.AsNoTracking()
            .Where(e => e.Id == persona.EstablecimientoId).Select(e => e.Nombre).FirstAsync(cancellationToken);
        var area = await db.Areas.AsNoTracking()
            .Where(a => a.Id == persona.AreaId).Select(a => a.Nombre).FirstAsync(cancellationToken);
        var sede = await db.Sedes.AsNoTracking()
            .Where(s => s.Id == persona.SedeId).Select(s => s.Nombre).FirstAsync(cancellationToken);

        var equipos = await (
            from x in db.Asignaciones.AsNoTracking()
            where x.PersonaId == persona.Id
            join a in db.Activos on x.ActivoId equals a.Id
            join t in db.TiposActivo on a.TipoActivoId equals t.Id
            join m in db.Marcas on a.MarcaId equals m.Id
            orderby x.FechaDevolucion == null descending, x.FechaEntrega descending, x.CreadoEn descending
            select new
            {
                x.Id,
                ActivoId = a.Id,
                a.CodigoPatrimonial,
                TipoActivo = t.Nombre,
                Marca = m.Nombre,
                a.Modelo,
                x.FechaEntrega,
                x.ConservacionEntrega,
                x.FechaDevolucion,
                x.ConservacionDevolucion,
            })
            .ToListAsync(cancellationToken);

        // Foto principal de cada equipo, para la miniatura (D38).
        var ids = equipos.Select(e => e.ActivoId).Distinct().ToList();
        var fotos = await db.FotosActivo.AsNoTracking()
            .Where(f => ids.Contains(f.ActivoId) && f.Principal)
            .Select(f => new { f.ActivoId, f.Id })
            .ToListAsync(cancellationToken);
        var principales = fotos.GroupBy(f => f.ActivoId).ToDictionary(g => g.Key, g => g.First().Id);

        return new PersonaDto(
            persona.Id,
            persona.Dni,
            persona.ApellidoPaterno,
            persona.ApellidoMaterno,
            persona.Nombres,
            persona.NombreCompleto,
            persona.Cargo,
            persona.EstablecimientoId,
            establecimiento,
            persona.AreaId,
            area,
            persona.Verificada,
            persona.VerificadaEn,
            persona.Activa,
            persona.SedeId,
            sede,
            equipos
                .Select(e => new EquipoPersonaDto(
                    e.Id,
                    e.ActivoId,
                    e.CodigoPatrimonial,
                    e.TipoActivo,
                    e.Marca,
                    e.Modelo,
                    principales.TryGetValue(e.ActivoId, out var foto) ? foto : null,
                    e.FechaEntrega,
                    e.ConservacionEntrega,
                    e.FechaDevolucion,
                    e.ConservacionDevolucion))
                .ToList());
    }
}
