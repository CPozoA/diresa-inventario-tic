using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Catalogos;
using Inventario.Domain.Seguridad;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Catalogos;

/// <summary>
/// Describe un catálogo: su código en la URL (<c>/api/catalogos/{codigo}</c>), sus nombres para la interfaz,
/// si es de sede o general, y cómo consultarlo. Todos los catálogos comparten los mismos casos de uso.
/// </summary>
internal abstract class DefinicionCatalogo(string codigo, string nombre, string nombreSingular, bool esDeSede, bool oculto = false)
{
    public string Codigo { get; } = codigo;

    public string Nombre { get; } = nombre;

    public string NombreSingular { get; } = nombreSingular;

    /// <summary>Verdadero si cada sede tiene el suyo (áreas).</summary>
    public bool EsDeSede { get; } = esDeSede;

    /// <summary>
    /// Catálogo sin uso que se conserva en la base de datos pero no se ofrece: no se lista ni se consulta ni se modifica
    /// por la API (por ejemplo, "Tipos de licencia" desde que las licencias son solo suscripciones, D54).
    /// </summary>
    public bool Oculto { get; } = oculto;

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
    Func<Guid?, string, T> crear,
    Expression<Func<T, ElementoCatalogoDto>>? proyeccion = null,
    bool oculto = false)
    : DefinicionCatalogo(codigo, nombre, nombreSingular, typeof(ElementoCatalogoDeSede).IsAssignableFrom(typeof(T)), oculto)
    where T : ElementoCatalogo
{
    private readonly Expression<Func<T, ElementoCatalogoDto>> _proyeccion =
        proyeccion ?? (e => new ElementoCatalogoDto(e.Id, e.Nombre, e.Activo, null, null, null));

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
            .Select(_proyeccion)
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
            "tipos-activo",
            "Tipos de activo",
            "Tipo de activo",
            db => db.TiposActivo,
            (_, n) => TipoActivo.Crear(n),
            t => new ElementoCatalogoDto(t.Id, t.Nombre, t.Activo, t.FichaAsignada ?? FichaTecnica.Ninguna, null, null)),
        new DefinicionCatalogo<Marca>("marcas", "Marcas", "Marca", db => db.Marcas, (_, n) => Marca.Crear(n)),
        new DefinicionCatalogo<EstadoActivo>(
            "estados-activo",
            "Estados del activo",
            "Estado",
            db => db.EstadosActivo,
            (_, n) => EstadoActivo.Crear(n),
            e => new ElementoCatalogoDto(e.Id, e.Nombre, e.Activo, null, null, e.GrupoAsignado ?? GrupoEstado.EnUso)),
        new DefinicionCatalogo<SistemaOperativo>(
            "sistemas-operativos", "Sistemas operativos", "Sistema operativo", db => db.SistemasOperativos,
            (_, n) => SistemaOperativo.Crear(n)),
        new DefinicionCatalogo<Procesador>(
            "procesadores",
            "Procesadores",
            "Procesador",
            db => db.Procesadores,
            (_, n) => Procesador.Crear(n),
            p => new ElementoCatalogoDto(p.Id, p.Nombre, p.Activo, null, p.Generaciones, null)),
        new DefinicionCatalogo<Antivirus>(
            "antivirus", "Antivirus o EDR", "Antivirus o EDR", db => db.Antivirus, (_, n) => Antivirus.Crear(n)),
        // Oculto desde el Paso 11: las licencias son solo suscripciones (D54). Se conserva por si se vuelve a usar.
        new DefinicionCatalogo<TipoLicencia>(
            "tipos-licencia", "Tipos de licencia", "Tipo de licencia", db => db.TiposLicencia, (_, n) => TipoLicencia.Crear(n), oculto: true),
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

    /// <summary>Catálogos que se ofrecen (sin los ocultos).</summary>
    public static IReadOnlyList<DefinicionCatalogo> Visibles { get; } = Todas.Where(d => !d.Oculto).ToList();

    /// <summary>El catálogo con ese código, o nulo si no existe o está oculto.</summary>
    public static DefinicionCatalogo? Buscar(string? codigo) =>
        Visibles.FirstOrDefault(d => string.Equals(d.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
}
