using Inventario.Domain.Common;

namespace Inventario.Domain.Seguridad;

/// <summary>
/// Conjunto de permisos que se asigna a usuarios.
/// Hay dos roles del sistema (Superadministrador y Administrador de sede), cuyos permisos se calculan en código
/// y no se pueden modificar, y roles de sede, que cada sede crea y combina libremente.
/// Ver docs/ARQUITECTURA.md §6.3.
/// </summary>
public sealed class Rol : EntidadAuditable
{
    public const int LongitudMaximaNombre = 60;
    public const int LongitudMaximaDescripcion = 250;

    private readonly List<RolPermiso> _permisos = [];

    // Requerido por EF Core.
    private Rol()
    {
    }

    private Rol(Guid id, TipoRol tipo, Guid? sedeId, string nombre, string? descripcion)
        : base(id)
    {
        Tipo = tipo;
        SedeId = sedeId;
        Nombre = nombre;
        Descripcion = descripcion;
    }

    public TipoRol Tipo { get; private set; }

    /// <summary>Sede dueña del rol. Es nulo en los roles del sistema.</summary>
    public Guid? SedeId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string? Descripcion { get; private set; }

    public bool EsDelSistema => Tipo != TipoRol.DeSede;

    /// <summary>Permisos guardados para un rol de sede. Los roles del sistema no guardan permisos.</summary>
    public IReadOnlyCollection<RolPermiso> PermisosAsignados => _permisos.AsReadOnly();

    public static Rol CrearDeSede(Guid sedeId, string nombre, string? descripcion, IEnumerable<string> codigosPermiso)
    {
        if (sedeId == Guid.Empty)
        {
            throw new DominioException("Un rol de sede debe indicar su sede.");
        }

        var rol = new Rol(Guid.CreateVersion7(), TipoRol.DeSede, sedeId, ValidarNombre(nombre), ValidarDescripcion(descripcion));
        rol.DefinirPermisos(codigosPermiso);
        return rol;
    }

    internal static Rol CrearDelSistema(Guid id, TipoRol tipo, string nombre, string descripcion)
    {
        if (tipo == TipoRol.DeSede)
        {
            throw new DominioException("Un rol del sistema no puede ser de tipo sede.");
        }

        return new Rol(id, tipo, sedeId: null, nombre, descripcion);
    }

    public void CambiarDatos(string nombre, string? descripcion)
    {
        ExigirEditable();
        Nombre = ValidarNombre(nombre);
        Descripcion = ValidarDescripcion(descripcion);
    }

    /// <summary>
    /// Reemplaza los permisos del rol. Rechaza códigos inexistentes y permisos exclusivos del superadministrador.
    /// La regla "solo se otorgan permisos que quien edita ya tiene" depende de quién edita y se valida en Application.
    /// </summary>
    public void DefinirPermisos(IEnumerable<string> codigosPermiso)
    {
        ArgumentNullException.ThrowIfNull(codigosPermiso);
        ExigirEditable();

        var nuevos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var codigo in codigosPermiso)
        {
            var definicion = Permisos.Buscar(codigo)
                ?? throw new DominioException($"El permiso '{codigo}' no existe.");

            if (definicion.ExclusivoSuperAdmin)
            {
                throw new DominioException($"El permiso '{codigo}' es exclusivo del superadministrador.");
            }

            nuevos.Add(definicion.Codigo);
        }

        _permisos.RemoveAll(p => !nuevos.Contains(p.CodigoPermiso));

        foreach (var codigo in nuevos)
        {
            if (!_permisos.Exists(p => p.CodigoPermiso == codigo))
            {
                _permisos.Add(new RolPermiso(Id, codigo));
            }
        }
    }

    /// <summary>Permisos que otorga este rol.</summary>
    public IReadOnlySet<string> PermisosEfectivos() => Tipo switch
    {
        TipoRol.SuperAdmin => Permisos.CodigosTodos,
        TipoRol.AdministradorSede => Permisos.DeSede,
        _ => _permisos.Select(p => p.CodigoPermiso).ToHashSet(StringComparer.Ordinal),
    };

    private void ExigirEditable()
    {
        if (EsDelSistema)
        {
            throw new DominioException("Los roles del sistema no se pueden modificar.");
        }
    }

    private static string ValidarNombre(string nombre) =>
        Guardas.TextoObligatorio(nombre, "El nombre del rol", LongitudMaximaNombre);

    private static string? ValidarDescripcion(string? descripcion) =>
        Guardas.TextoOpcional(descripcion, "La descripción del rol", LongitudMaximaDescripcion);
}
