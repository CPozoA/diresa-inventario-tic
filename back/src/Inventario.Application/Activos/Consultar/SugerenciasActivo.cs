using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos.Consultar;

/// <summary>Campos escritos de la ficha que sugieren lo ya registrado (D45).</summary>
public enum CampoSugerencia
{
    Modelo,
    Proveedor,
    DominioGrupo,
    VersionSistemaOperativo,
    UbicacionFisica,
    MarcaModeloDisco,
    MarcaModeloRam,
}

/// <summary>
/// Valores ya registrados en la red para un campo escrito, los más usados primero: así "Latitude 5440" no se vuelve a
/// escribir como "latitude5440". El campo sigue aceptando un valor nuevo.
/// </summary>
/// <param name="Campo">Campo a sugerir.</param>
/// <param name="Texto">Lo escrito hasta ahora (vacío: los más usados).</param>
/// <param name="MarcaId">Para el modelo: solo los de esa marca.</param>
/// <param name="SistemaOperativoId">Para la versión: solo las de ese sistema.</param>
/// <param name="EstablecimientoId">Para la ubicación física: solo las de ese establecimiento.</param>
/// <remarks>Requiere Activos.Ver. Un usuario de sede solo recibe valores de su red.</remarks>
public sealed record SugerenciasActivoQuery(
    CampoSugerencia Campo,
    string? Texto,
    Guid? MarcaId,
    Guid? SistemaOperativoId,
    Guid? EstablecimientoId) : IQuery<IReadOnlyList<string>>;

internal sealed class SugerenciasActivoHandler(IAppDbContext db) : IQueryHandler<SugerenciasActivoQuery, IReadOnlyList<string>>
{
    public const int Maximo = 10;

    public async Task<Result<IReadOnlyList<string>>> Handle(SugerenciasActivoQuery query, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(query.Campo))
        {
            return Array.Empty<string>();
        }

        // El filtro global de sede ya limita a un usuario de sede a la suya.
        var activos = db.Activos.AsNoTracking();
        if (query.MarcaId is Guid marcaId && query.Campo == CampoSugerencia.Modelo)
        {
            activos = activos.Where(a => a.MarcaId == marcaId);
        }

        if (query.SistemaOperativoId is Guid sistemaId && query.Campo == CampoSugerencia.VersionSistemaOperativo)
        {
            activos = activos.Where(a => a.SistemaOperativoId == sistemaId);
        }

        if (query.EstablecimientoId is Guid establecimientoId && query.Campo == CampoSugerencia.UbicacionFisica)
        {
            activos = activos.Where(a => a.EstablecimientoId == establecimientoId);
        }

        IQueryable<string?> valores = query.Campo switch
        {
            CampoSugerencia.Modelo => activos.Select(a => (string?)a.Modelo),
            CampoSugerencia.Proveedor => activos.Select(a => a.Proveedor),
            CampoSugerencia.DominioGrupo => activos.Select(a => a.DominioGrupo),
            CampoSugerencia.VersionSistemaOperativo => activos.Select(a => a.VersionSistemaOperativo),
            CampoSugerencia.UbicacionFisica => activos.Select(a => a.UbicacionFisica),
            CampoSugerencia.MarcaModeloDisco => activos.SelectMany(a => a.Discos).Select(d => d.MarcaModelo),
            CampoSugerencia.MarcaModeloRam => activos.SelectMany(a => a.ModulosRam).Select(m => m.MarcaModelo),
            _ => throw new InvalidOperationException($"Campo de sugerencia no previsto: {query.Campo}."),
        };

        var texto = query.Texto?.Trim();
        var conValor = valores.Where(v => v != null && v != string.Empty);
        if (!string.IsNullOrEmpty(texto))
        {
            conValor = conValor.Where(v => v!.Contains(texto));
        }

        var lista = await conValor
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key!)
            .Take(Maximo)
            .ToListAsync(cancellationToken);

        return lista;
    }
}
