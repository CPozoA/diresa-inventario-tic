using System.Globalization;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Establecimientos.Listar;

/// <summary>Establecimiento tal como lo ve la interfaz.</summary>
/// <param name="Id">Clave interna.</param>
/// <param name="SedeId">Red (sede) a la que pertenece.</param>
/// <param name="SedeNombre">Nombre de la red, para la vista consolidada del superadministrador.</param>
/// <param name="IdEstablecimiento">Identificador de la lista oficial.</param>
/// <param name="CodigoUnico">Código único.</param>
/// <param name="Nombre">Nombre.</param>
/// <param name="Categoria">Categoría (II-1, I-3…).</param>
/// <param name="Microred">Microred.</param>
/// <param name="Provincia">Provincia.</param>
/// <param name="Distrito">Distrito.</param>
/// <param name="Ubigeo">Ubigeo de 6 dígitos.</param>
/// <param name="Activo">Falso si ya no se ofrece al registrar activos.</param>
public sealed record EstablecimientoDto(
    Guid Id,
    Guid SedeId,
    string SedeNombre,
    int IdEstablecimiento,
    int CodigoUnico,
    string Nombre,
    string? Categoria,
    string? Microred,
    string? Provincia,
    string? Distrito,
    string? Ubigeo,
    bool Activo);

/// <summary>
/// Establecimientos ordenados por nombre. Cualquier usuario con sesión puede consultarlos (los necesita al registrar
/// activos); un usuario de sede solo ve los de su red. El superadministrador puede filtrar por sede.
/// </summary>
/// <param name="SedeId">Solo para el superadministrador: limita a una red.</param>
/// <param name="Buscar">Código único, Id_Establecimiento exacto, o parte del nombre.</param>
/// <param name="IncluirInactivos">Falso (por omisión) para ofrecerlos al registrar; verdadero para administrarlos.</param>
/// <param name="Maximo">Cantidad máxima de resultados (para las búsquedas mientras se escribe).</param>
/// <param name="SinActivos">Verdadero: solo los que todavía no tienen ningún activo registrado (resumen del inicio).</param>
public sealed record ListarEstablecimientosQuery(Guid? SedeId, string? Buscar, bool IncluirInactivos, int? Maximo, bool SinActivos = false)
    : IQuery<IReadOnlyList<EstablecimientoDto>>;

internal sealed class ListarEstablecimientosHandler(IAppDbContext db)
    : IQueryHandler<ListarEstablecimientosQuery, IReadOnlyList<EstablecimientoDto>>
{
    public async Task<Result<IReadOnlyList<EstablecimientoDto>>> Handle(
        ListarEstablecimientosQuery query,
        CancellationToken cancellationToken)
    {
        // El filtro global de sede ya limita a un usuario de sede a su red; el parámetro solo acota más.
        var consulta = db.Establecimientos.AsNoTracking();

        if (query.SedeId is Guid sedeId)
        {
            consulta = consulta.Where(e => e.SedeId == sedeId);
        }

        if (!query.IncluirInactivos)
        {
            consulta = consulta.Where(e => e.Activo);
        }

        if (query.SinActivos)
        {
            consulta = consulta.Where(e => !db.Activos.Any(a => a.EstablecimientoId == e.Id));
        }

        var buscar = query.Buscar?.Trim();
        if (!string.IsNullOrEmpty(buscar))
        {
            consulta = int.TryParse(buscar, NumberStyles.None, CultureInfo.InvariantCulture, out var numero)
                ? consulta.Where(e => e.CodigoUnico == numero || e.IdEstablecimiento == numero || e.Nombre.Contains(buscar))
                : consulta.Where(e => e.Nombre.Contains(buscar));
        }

        // Se une con la sede antes de ordenar y acotar, para que el orden se conserve en la consulta final.
        var ordenada = consulta
            .Join(db.Sedes, e => e.SedeId, s => s.Id, (e, s) => new { Establecimiento = e, SedeNombre = s.Nombre })
            .OrderBy(x => x.Establecimiento.Nombre)
            .ThenBy(x => x.Establecimiento.CodigoUnico);

        var acotada = query.Maximo is int maximo and > 0 ? ordenada.Take(maximo) : ordenada;

        var lista = await acotada
            .Select(x => new EstablecimientoDto(
                x.Establecimiento.Id,
                x.Establecimiento.SedeId,
                x.SedeNombre,
                x.Establecimiento.IdEstablecimiento,
                x.Establecimiento.CodigoUnico,
                x.Establecimiento.Nombre,
                x.Establecimiento.Categoria,
                x.Establecimiento.Microred,
                x.Establecimiento.Provincia,
                x.Establecimiento.Distrito,
                x.Establecimiento.Ubigeo,
                x.Establecimiento.Activo))
            .ToListAsync(cancellationToken);

        return Result.Exito<IReadOnlyList<EstablecimientoDto>>(lista);
    }
}
