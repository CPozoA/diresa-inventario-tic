using Inventario.Domain.Common;

namespace Inventario.Domain.Catalogos;

// ---------- Catálogos de cada sede (docs/ARQUITECTURA.md D9) ----------

/// <summary>Área o dependencia de la sede (Logística, Contabilidad, Tecnologías de la Información…).</summary>
public sealed class Area : ElementoCatalogoDeSede
{
    private Area()
    {
    }

    private Area(Guid sedeId, string nombre)
        : base(sedeId, nombre)
    {
    }

    public static Area Crear(Guid sedeId, string nombre) => new(sedeId, nombre);
}

// ---------- Catálogos generales: iguales para todas las sedes; solo los administra el superadministrador ----------

/// <summary>
/// Qué características técnicas lleva un tipo de activo (docs/ARQUITECTURA.md D43): decide qué secciones muestra
/// la ficha y qué datos acepta la API.
/// </summary>
public enum FichaTecnica
{
    /// <summary>Sin características técnicas (monitor, UPS, proyector…).</summary>
    Ninguna,

    /// <summary>Solo datos de red: nombre de equipo, IP y MAC (impresora, switch, router…).</summary>
    Red,

    /// <summary>Equipo de cómputo: todas las características, discos y memoria RAM.</summary>
    Computo,
}

/// <summary>Tipo de activo (Laptop, Monitor, Switch…), con la ficha técnica que le corresponde.</summary>
public sealed class TipoActivo : ElementoCatalogo
{
    private TipoActivo()
    {
    }

    private TipoActivo(string nombre)
        : base(nombre)
    {
        FichaAsignada = CatalogosIniciales.FichaPorOmision(Nombre);
    }

    /// <summary>
    /// Ficha guardada. Es nula solo en los tipos registrados antes de que existiera este dato: al arrancar, el sistema
    /// les asigna la que corresponde por su nombre (<see cref="CatalogosIniciales.FichaPorOmision"/>).
    /// </summary>
    public FichaTecnica? FichaAsignada { get; private set; }

    /// <summary>Ficha técnica del tipo.</summary>
    public FichaTecnica Ficha => FichaAsignada ?? FichaTecnica.Ninguna;

    public static TipoActivo Crear(string nombre) => new(nombre);

    public void CambiarFicha(FichaTecnica ficha)
    {
        if (!Enum.IsDefined(ficha))
        {
            throw new DominioException("La ficha técnica no es válida.");
        }

        FichaAsignada = ficha;
    }

    /// <summary>Asigna la ficha por omisión a un tipo registrado antes de que existiera este dato. Devuelve si cambió.</summary>
    public bool CompletarFichaSiFalta()
    {
        if (FichaAsignada is not null)
        {
            return false;
        }

        FichaAsignada = CatalogosIniciales.FichaPorOmision(Nombre);
        return true;
    }
}

/// <summary>Marca o fabricante (HP, Dell, Cisco…).</summary>
public sealed class Marca : ElementoCatalogo
{
    private Marca()
    {
    }

    private Marca(string nombre)
        : base(nombre)
    {
    }

    public static Marca Crear(string nombre) => new(nombre);
}

/// <summary>
/// A qué grupo pertenece un estado del activo (docs/ARQUITECTURA.md D48). El resumen cuenta por grupo, así que un
/// estado se puede renombrar ("Operativo" → "En uso") sin que cambien las cifras.
/// </summary>
public enum GrupoEstado
{
    /// <summary>Funciona y se usa (Operativo).</summary>
    EnUso,

    /// <summary>En reparación o revisión, volverá a usarse.</summary>
    EnMantenimiento,

    /// <summary>No funciona (Inoperativo, Malogrado).</summary>
    FueraDeServicio,

    /// <summary>Funciona pero está guardado, sin asignar.</summary>
    EnAlmacen,

    /// <summary>Dado de baja: ya no se renueva ni cuenta en las alertas (D49).</summary>
    DeBaja,
}

/// <summary>Estado de un activo (Operativo, En mantenimiento, De baja…), con el grupo al que pertenece.</summary>
public sealed class EstadoActivo : ElementoCatalogo
{
    private EstadoActivo()
    {
    }

    private EstadoActivo(string nombre)
        : base(nombre)
    {
        GrupoAsignado = CatalogosIniciales.GrupoPorOmision(Nombre);
    }

    /// <summary>
    /// Grupo guardado. Es nulo solo en los estados registrados antes de que existiera este dato: al arrancar, el sistema
    /// les asigna el que corresponde por su nombre (<see cref="CatalogosIniciales.GrupoPorOmision"/>).
    /// </summary>
    public GrupoEstado? GrupoAsignado { get; private set; }

    /// <summary>Grupo del estado.</summary>
    public GrupoEstado Grupo => GrupoAsignado ?? GrupoEstado.EnUso;

    public static EstadoActivo Crear(string nombre) => new(nombre);

    public void CambiarGrupo(GrupoEstado grupo)
    {
        if (!Enum.IsDefined(grupo))
        {
            throw new DominioException("El grupo del estado no es válido.");
        }

        GrupoAsignado = grupo;
    }

    /// <summary>Asigna el grupo por omisión a un estado registrado antes de que existiera este dato. Devuelve si cambió.</summary>
    public bool CompletarGrupoSiFalta()
    {
        if (GrupoAsignado is not null)
        {
            return false;
        }

        GrupoAsignado = CatalogosIniciales.GrupoPorOmision(Nombre);
        return true;
    }
}

/// <summary>Sistema operativo instalado en un equipo.</summary>
public sealed class SistemaOperativo : ElementoCatalogo
{
    private SistemaOperativo()
    {
    }

    private SistemaOperativo(string nombre)
        : base(nombre)
    {
    }

    public static SistemaOperativo Crear(string nombre) => new(nombre);
}

/// <summary>
/// Qué generaciones ofrece una familia de procesadores (docs/ARQUITECTURA.md D44). Las listas están en
/// <see cref="GeneracionesDeProcesador"/>.
/// </summary>
public enum GeneracionesProcesador
{
    /// <summary>Sin generación (Celeron, Xeon, Apple M1…).</summary>
    Ninguna,

    /// <summary>Intel Core i3, i5, i7, i9: 1.ª a 14.ª generación.</summary>
    IntelCore,

    /// <summary>Intel Core Ultra: Serie 1 a Serie 3.</summary>
    IntelCoreUltra,

    /// <summary>AMD Ryzen: Serie 1000 a Serie 9000.</summary>
    AmdRyzen,
}

/// <summary>Familia de procesadores (Intel Core i5, AMD Ryzen 7…), con las generaciones que le corresponden.</summary>
public sealed class Procesador : ElementoCatalogo
{
    private Procesador()
    {
    }

    private Procesador(string nombre)
        : base(nombre)
    {
        Generaciones = CatalogosIniciales.GeneracionesPorOmision(Nombre);
    }

    public GeneracionesProcesador Generaciones { get; private set; }

    public static Procesador Crear(string nombre) => new(nombre);

    public void CambiarGeneraciones(GeneracionesProcesador generaciones)
    {
        if (!Enum.IsDefined(generaciones))
        {
            throw new DominioException("Las generaciones del procesador no son válidas.");
        }

        Generaciones = generaciones;
    }
}

/// <summary>Antivirus o EDR instalado en un equipo (D45).</summary>
public sealed class Antivirus : ElementoCatalogo
{
    private Antivirus()
    {
    }

    private Antivirus(string nombre)
        : base(nombre)
    {
    }

    public static Antivirus Crear(string nombre) => new(nombre);
}

/// <summary>Modalidad de una licencia de software (Perpetua, Suscripción anual, OEM…).</summary>
public sealed class TipoLicencia : ElementoCatalogo
{
    private TipoLicencia()
    {
    }

    private TipoLicencia(string nombre)
        : base(nombre)
    {
    }

    public static TipoLicencia Crear(string nombre) => new(nombre);
}
