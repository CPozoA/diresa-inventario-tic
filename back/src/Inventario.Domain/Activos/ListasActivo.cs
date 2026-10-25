namespace Inventario.Domain.Activos;

/// <summary>Estado físico del equipo.</summary>
public enum Conservacion
{
    Nuevo,
    Bueno,
    Regular,
    Malo,
}

/// <summary>Qué tan grave sería que el equipo falle.</summary>
public enum Criticidad
{
    Alta,
    Media,
    Baja,
}

/// <summary>Cómo llegó el equipo a la sede.</summary>
public enum TipoAdquisicion
{
    Compra,
    Arrendamiento,
    Donacion,
    Transferencia,
    Comodato,
}

/// <summary>Tecnología del disco.</summary>
public enum TipoDisco
{
    SsdSata,
    Nvme,
    Hdd,
    Hibrido,
}

/// <summary>Para qué se usa el disco.</summary>
public enum UsoDisco
{
    Sistema,
    Datos,
    Respaldo,
}

/// <summary>Tipo de memoria RAM.</summary>
public enum TipoRam
{
    Ddr3,
    Ddr3L,
    Ddr4,
    Ddr5,
    Lpddr4X,
    Lpddr5,
}

/// <summary>Estado de la garantía a una fecha.</summary>
public enum EstadoGarantia
{
    SinDato,
    Vigente,
    Vencida,
}

/// <summary>Situación del equipo según su vida útil.</summary>
public enum SituacionRenovacion
{
    SinDato,
    Vigente,
    ProximoARenovar,
    Renovar,
}

/// <summary>
/// Listas fijas de la ficha técnica (docs/ARQUITECTURA.md D41): se eligen, no se escriben, para evitar errores
/// de registro. La API las entrega a la interfaz con sus textos.
/// </summary>
public static class ListasActivo
{
    /// <summary>Un equipo pasa a "Próximo a renovar" cuando le queda este tiempo de vida útil (o menos).</summary>
    public const int AniosAvisoRenovacion = 1;

    public const int MaximoDiscos = 16;
    public const int MaximoModulosRam = 32;

    /// <summary>Capacidad máxima aceptada para un disco (100 TB), contra errores de tipeo.</summary>
    public const int CapacidadMaximaDiscoGb = 100_000;

    /// <summary>Capacidades habituales de disco, en GB. La interfaz ofrece además "Otra" para escribir un valor.</summary>
    public static IReadOnlyList<int> CapacidadesDiscoGb { get; } =
        [120, 128, 240, 250, 256, 320, 480, 500, 512, 960, 1000, 1024, 2000, 2048, 4000, 4096, 8000, 8192, 16000];

    /// <summary>Capacidades de un módulo de RAM, en GB. Solo se aceptan estas.</summary>
    public static IReadOnlyList<int> CapacidadesRamGb { get; } = [1, 2, 4, 8, 12, 16, 24, 32, 48, 64];

    /// <summary>Velocidades (MHz) que corresponden a cada tipo de RAM. Solo se aceptan las del tipo elegido.</summary>
    public static IReadOnlyDictionary<TipoRam, IReadOnlyList<int>> VelocidadesRamMhz { get; } =
        new Dictionary<TipoRam, IReadOnlyList<int>>
        {
            [TipoRam.Ddr3] = [800, 1066, 1333, 1600, 1866, 2133],
            [TipoRam.Ddr3L] = [1066, 1333, 1600, 1866],
            [TipoRam.Ddr4] = [2133, 2400, 2666, 2933, 3200],
            [TipoRam.Ddr5] = [4800, 5200, 5600, 6000, 6400],
            [TipoRam.Lpddr4X] = [3733, 4266],
            [TipoRam.Lpddr5] = [5500, 6400],
        };

    /// <summary>Texto para mostrar de cada valor.</summary>
    public static string Texto(Enum valor) =>
        (object)valor switch
        {
            TipoAdquisicion.Arrendamiento => "Arrendamiento (leasing)",
            TipoAdquisicion.Donacion => "Donación",
            TipoDisco.SsdSata => "SSD SATA",
            TipoDisco.Nvme => "NVMe",
            TipoDisco.Hdd => "HDD",
            TipoDisco.Hibrido => "Híbrido (SSHD)",
            TipoRam.Ddr3 => "DDR3",
            TipoRam.Ddr3L => "DDR3L",
            TipoRam.Ddr4 => "DDR4",
            TipoRam.Ddr5 => "DDR5",
            TipoRam.Lpddr4X => "LPDDR4X",
            TipoRam.Lpddr5 => "LPDDR5",
            EstadoGarantia.SinDato or SituacionRenovacion.SinDato => "Sin dato",
            SituacionRenovacion.ProximoARenovar => "Próximo a renovar",
            _ => valor.ToString(),
        };
}
