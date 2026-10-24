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

/// <summary>Lugar físico de la sede (Piso 2 – Oficina 204, Almacén central…).</summary>
public sealed class Ubicacion : ElementoCatalogoDeSede
{
    private Ubicacion()
    {
    }

    private Ubicacion(Guid sedeId, string nombre)
        : base(sedeId, nombre)
    {
    }

    public static Ubicacion Crear(Guid sedeId, string nombre) => new(sedeId, nombre);
}

// ---------- Catálogos generales: iguales para todas las sedes; solo los administra el superadministrador ----------

/// <summary>Tipo de activo (Laptop, Monitor, Switch…).</summary>
public sealed class TipoActivo : ElementoCatalogo
{
    private TipoActivo()
    {
    }

    private TipoActivo(string nombre)
        : base(nombre)
    {
    }

    public static TipoActivo Crear(string nombre) => new(nombre);
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
