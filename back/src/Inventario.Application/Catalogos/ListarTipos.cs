using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;

namespace Inventario.Application.Catalogos;

/// <summary>Un catálogo disponible y si quien consulta puede modificarlo.</summary>
/// <param name="Codigo">Código en la URL, por ejemplo <c>tipos-activo</c>.</param>
/// <param name="Nombre">Nombre en plural para la interfaz.</param>
/// <param name="NombreSingular">Nombre de un elemento (para "Nueva marca", etc.).</param>
/// <param name="EsDeSede">Verdadero si cada sede tiene el suyo (áreas).</param>
/// <param name="PuedeGestionar">Si quien consulta puede crear, renombrar o desactivar elementos.</param>
public sealed record TipoCatalogoDto(string Codigo, string Nombre, string NombreSingular, bool EsDeSede, bool PuedeGestionar);

/// <summary>Lista los catálogos del sistema.</summary>
public sealed record ListarTiposCatalogoQuery : IQuery<IReadOnlyList<TipoCatalogoDto>>;

internal sealed class ListarTiposCatalogoHandler(IPermisosUsuarioActual permisos)
    : IQueryHandler<ListarTiposCatalogoQuery, IReadOnlyList<TipoCatalogoDto>>
{
    public async Task<Result<IReadOnlyList<TipoCatalogoDto>>> Handle(ListarTiposCatalogoQuery query, CancellationToken cancellationToken)
    {
        var propios = await permisos.ObtenerAsync(cancellationToken);

        var lista = DefinicionesCatalogo.Todas
            .Select(d => new TipoCatalogoDto(d.Codigo, d.Nombre, d.NombreSingular, d.EsDeSede, propios.Contains(d.PermisoGestion)))
            .ToList();

        return Result.Exito<IReadOnlyList<TipoCatalogoDto>>(lista);
    }
}
