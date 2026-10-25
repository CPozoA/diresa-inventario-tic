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

/// <summary>Estado de un activo (Operativo, En mantenimiento, De baja…).</summary>
public sealed class EstadoActivo : ElementoCatalogo
{
    private EstadoActivo()
    {
    }

    private EstadoActivo(string nombre)
        : base(nombre)
    {
    }

    public static EstadoActivo Crear(string nombre) => new(nombre);
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
