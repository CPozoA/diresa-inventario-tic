using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Catalogos;

/// <summary>
/// Describe un catálogo: su código en la URL (<c>/api/catalogos/{codigo}</c>), sus nombres para la interfaz,
/// si es de sede o general, y cómo consultarlo. Todos los catálogos comparten los mismos casos de uso.
/// </summary>
internal abstract class DefinicionCatalogo(string codigo, string nombre, string nombreSingular, bool esDeSede)
{
    public string Codigo { get; } = codigo;

    public string Nombre { get; } = nombre;

    public string NombreSingular { get; } = nombreSingular;

    /// <summary>Verdadero si cada sede tiene el suyo (áreas).</summary>
    public bool EsDeSede { get; } = esDeSede;

    /// <summary>Permiso para crear, renombrar, activar o desactivar elementos.</summary>
    public string PermisoGestion => EsDeSede ? Permisos.Catalogos.GestionarSede : Permisos.Catalogos.GestionarGlobales;

    /// <param name="db">Contexto.</param>
    /// <param name="sedeId">Sede del catálogo (solo catálogos de sede).</param>
    /// <param name="incluirInactivos">Si se incluyen los desactivados.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    public abstract Task<List<ElementoCatalogoDto>> ListarAsync(
        IAppDbContext db, Guid? sedeId, bool incluirInactivos, CancellationToken cancellationToken);

    /// <summary>Verdadero si ya existe otro elemento con ese nombre (en la misma sede, si es de sede).</summary>
    public abstract Task<bool> ExisteNombreAsync(
        IAppDbContext db, Guid? sedeId, string nombre, Guid? excluirId, CancellationToken cancellationToken);

    /// <summary>Elemento con seguimiento de cambios. El filtro de sede oculta los de otras sedes.</summary>
    public abstract Task<ElementoCatalogo?> BuscarAsync(IAppDbContext db, Guid id, CancellationToken cancellationToken);

    /// <summary>Crea el elemento (el dominio valida el nombre) y lo agrega al contexto.</summary>
    public abstract ElementoCatalogo Agregar(IAppDbContext db, Guid? sedeId, string nombre);
}

/// <summary>Implementación para un tipo de elemento concreto. Las consultas son sobre <typeparamref name="T"/>, sin conversiones.</summary>
internal sealed class DefinicionCatalogo<T>(
    string codigo,
    string nombre,
    string nombreSingular,
    Func<IAppDbContext, DbSet<T>> tabla,
    Func<Guid?, string, T> crear)
    : DefinicionCatalogo(codigo, nombre, nombreSingular, typeof(ElementoCatalogoDeSede).IsAssignableFrom(typeof(T)))
    where T : ElementoCatalogo
{
    public override Task<List<ElementoCatalogoDto>> ListarAsync(
        IAppDbContext db, Guid? sedeId, bool incluirInactivos, CancellationToken cancellationToken)
    {
        var consulta = DeLaSede(db, sedeId).AsNoTracking();
        if (!incluirInactivos)
        {
            consulta = consulta.Where(e => e.Activo);
        }

        return consulta
            .OrderBy(e => e.Nombre)
            .Select(e => new ElementoCatalogoDto(e.Id, e.Nombre, e.Activo))
            .ToListAsync(cancellationToken);
    }

    public override Task<bool> ExisteNombreAsync(
        IAppDbContext db, Guid? sedeId, string nombre, Guid? excluirId, CancellationToken cancellationToken) =>
        DeLaSede(db, sedeId).AnyAsync(e => e.Nombre == nombre && e.Id != excluirId, cancellationToken);

    public override async Task<ElementoCatalogo?> BuscarAsync(IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        await tabla(db).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public override ElementoCatalogo Agregar(IAppDbContext db, Guid? sedeId, string nombre)
    {
        var elemento = crear(sedeId, nombre);
        tabla(db).Add(elemento);
        return elemento;
    }

    private IQueryable<T> DeLaSede(IAppDbContext db, Guid? sedeId) =>
        EsDeSede && sedeId is Guid sede
            ? tabla(db).Where(e => EF.Property<Guid>(e, nameof(ElementoCatalogoDeSede.SedeId)) == sede)
            : tabla(db);
}

internal static class DefinicionesCatalogo
{
    public static IReadOnlyList<DefinicionCatalogo> Todas { get; } =
    [
        new DefinicionCatalogo<Area>("areas", "Áreas", "Área", db => db.Areas, (sede, n) => Area.Crear(sede ?? Guid.Empty, n)),
        new DefinicionCatalogo<TipoActivo>(
            "tipos-activo", "Tipos de activo", "Tipo de activo", db => db.TiposActivo, (_, n) => TipoActivo.Crear(n)),
        new DefinicionCatalogo<Marca>("marcas", "Marcas", "Marca", db => db.Marcas, (_, n) => Marca.Crear(n)),
        new DefinicionCatalogo<EstadoActivo>(
            "estados-activo", "Estados del activo", "Estado", db => db.EstadosActivo, (_, n) => EstadoActivo.Crear(n)),
        new DefinicionCatalogo<SistemaOperativo>(
            "sistemas-operativos", "Sistemas operativos", "Sistema operativo", db => db.SistemasOperativos,
            (_, n) => SistemaOperativo.Crear(n)),
        new DefinicionCatalogo<TipoLicencia>(
            "tipos-licencia", "Tipos de licencia", "Tipo de licencia", db => db.TiposLicencia, (_, n) => TipoLicencia.Crear(n)),
    ];

    public static readonly ErrorAplicacion TipoNoExiste =
        ErrorAplicacion.NoEncontrado("Catalogos.TipoNoExiste", "El catálogo no existe.");

    public static readonly ErrorAplicacion ElementoNoEncontrado =
        ErrorAplicacion.NoEncontrado("Catalogos.NoEncontrado", "El elemento no existe.");

    public static readonly ErrorAplicacion SinPermiso =
        ErrorAplicacion.Prohibido("Catalogos.SinPermiso", "No tiene permiso para modificar este catálogo.");

    public static ErrorAplicacion NombreDuplicado(DefinicionCatalogo definicion)
    {
        ArgumentNullException.ThrowIfNull(definicion);
        return ErrorAplicacion.Conflicto(
            "Catalogos.NombreDuplicado",
            $"Ya existe un elemento con ese nombre en el catálogo «{definicion.Nombre}».");
    }

    public static DefinicionCatalogo? Buscar(string? codigo) =>
        Todas.FirstOrDefault(d => string.Equals(d.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
}
