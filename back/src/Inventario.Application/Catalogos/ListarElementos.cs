using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Catalogos;

namespace Inventario.Application.Catalogos;

/// <summary>Elemento de un catálogo.</summary>
/// <param name="Id">Identificador.</param>
/// <param name="Nombre">Nombre.</param>
/// <param name="Activo">Falso si ya no se ofrece al registrar.</param>
/// <param name="Ficha">Solo en tipos de activo: qué características técnicas lleva (D43). Nulo en los demás catálogos.</param>
/// <param name="Generaciones">Solo en procesadores: qué generaciones ofrece (D44). Nulo en los demás catálogos.</param>
/// <param name="Grupo">Solo en estados del activo: a qué grupo pertenece (D48). Nulo en los demás catálogos.</param>
public sealed record ElementoCatalogoDto(
    Guid Id,
    string Nombre,
    bool Activo,
    FichaTecnica? Ficha,
    GeneracionesProcesador? Generaciones,
    GrupoEstado? Grupo);

/// <summary>
/// Elementos de un catálogo, ordenados por nombre. Cualquier usuario con sesión puede consultarlos
/// (los necesitará al registrar activos). En los catálogos de sede, el superadministrador indica la sede.
/// </summary>
/// <param name="Tipo">Código del catálogo, por ejemplo <c>marcas</c>.</param>
/// <param name="SedeId">Solo para catálogos de sede consultados por el superadministrador.</param>
/// <param name="IncluirInactivos">Falso (por omisión) para ofrecerlos al registrar; verdadero para administrarlos.</param>
public sealed record ListarElementosCatalogoQuery(string Tipo, Guid? SedeId, bool IncluirInactivos)
    : IQuery<IReadOnlyList<ElementoCatalogoDto>>;

internal sealed class ListarElementosCatalogoHandler(IAppDbContext db, ReglasDeGestion reglas)
    : IQueryHandler<ListarElementosCatalogoQuery, IReadOnlyList<ElementoCatalogoDto>>
{
    public async Task<Result<IReadOnlyList<ElementoCatalogoDto>>> Handle(
        ListarElementosCatalogoQuery query,
        CancellationToken cancellationToken)
    {
        var definicion = DefinicionesCatalogo.Buscar(query.Tipo);
        if (definicion is null)
        {
            return Result.Fallo<IReadOnlyList<ElementoCatalogoDto>>(DefinicionesCatalogo.TipoNoExiste);
        }

        Guid? sedeId = null;
        if (definicion.EsDeSede)
        {
            var sede = await reglas.ResolverSedeAsync(query.SedeId, cancellationToken);
            if (!sede.EsExito)
            {
                return Result.Fallo<IReadOnlyList<ElementoCatalogoDto>>(sede.Error!);
            }

            sedeId = sede.Valor;
        }

        var lista = await definicion.ListarAsync(db, sedeId, query.IncluirInactivos, cancellationToken);

        return Result.Exito<IReadOnlyList<ElementoCatalogoDto>>(lista);
    }
}
