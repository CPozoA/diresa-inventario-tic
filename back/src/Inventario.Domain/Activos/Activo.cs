using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Inventario.Domain.Common;

namespace Inventario.Domain.Activos;

/// <summary>Identificación, ubicación y estado del activo (docs/ARQUITECTURA.md §5.10).</summary>
/// <param name="CodigoPatrimonial">Código patrimonial o interno. Único dentro de la sede.</param>
/// <param name="TipoActivoId">Catálogo de tipos de activo.</param>
/// <param name="MarcaId">Catálogo de marcas.</param>
/// <param name="Modelo">Modelo.</param>
/// <param name="NumeroSerie">Número de serie.</param>
/// <param name="Descripcion">Descripción breve.</param>
/// <param name="AreaId">Área de la sede.</param>
/// <param name="UbicacionFisica">Dónde está (texto libre, D40).</param>
/// <param name="Responsable">Responsable o usuario del equipo (texto, hasta que exista el módulo de asignaciones).</param>
/// <param name="EstadoActivoId">Catálogo de estados.</param>
/// <param name="Conservacion">Estado físico.</param>
/// <param name="Criticidad">Criticidad.</param>
/// <param name="Observaciones">Observaciones.</param>
public sealed record DatosGeneralesActivo(
    string CodigoPatrimonial,
    Guid TipoActivoId,
    Guid MarcaId,
    string Modelo,
    string? NumeroSerie,
    string? Descripcion,
    Guid AreaId,
    string? UbicacionFisica,
    string? Responsable,
    Guid EstadoActivoId,
    Conservacion Conservacion,
    Criticidad? Criticidad,
    string? Observaciones);

/// <summary>Adquisición y garantía.</summary>
/// <param name="FechaAdquisicion">Fecha de adquisición.</param>
/// <param name="Proveedor">Proveedor (razón social).</param>
/// <param name="NumeroOrdenCompra">N° de orden de compra o factura.</param>
/// <param name="TipoAdquisicion">Cómo se adquirió.</param>
/// <param name="ValorAdquisicion">Valor en soles.</param>
/// <param name="VidaUtilAnios">Vida útil en años.</param>
/// <param name="FinGarantia">Fecha en que termina la garantía.</param>
public sealed record DatosAdquisicionActivo(
    DateOnly? FechaAdquisicion,
    string? Proveedor,
    string? NumeroOrdenCompra,
    TipoAdquisicion? TipoAdquisicion,
    decimal? ValorAdquisicion,
    int? VidaUtilAnios,
    DateOnly? FinGarantia)
{
    public static DatosAdquisicionActivo Vacios { get; } = new(null, null, null, null, null, null, null);
}

/// <summary>Características técnicas (equipos de cómputo; IP y MAC sirven también para equipos de red).</summary>
/// <param name="Procesador">Procesador.</param>
/// <param name="VelocidadGeneracion">Velocidad o generación del procesador.</param>
/// <param name="Nucleos">Número de núcleos.</param>
/// <param name="TarjetaGrafica">Tarjeta gráfica.</param>
/// <param name="PantallaPulgadas">Tamaño de pantalla.</param>
/// <param name="SistemaOperativoId">Catálogo de sistemas operativos.</param>
/// <param name="VersionSistemaOperativo">Versión del sistema operativo (23H2…).</param>
/// <param name="NombreEquipo">Nombre de equipo (hostname).</param>
/// <param name="DominioGrupo">Dominio o grupo de trabajo.</param>
/// <param name="DireccionIp">Dirección IP.</param>
/// <param name="DireccionMac">Dirección MAC.</param>
/// <param name="Antivirus">Antivirus o EDR.</param>
/// <param name="DiscoCifrado">Si el disco está cifrado.</param>
/// <param name="PuertosPrincipales">Puertos principales.</param>
/// <param name="AccesoriosIncluidos">Accesorios incluidos.</param>
public sealed record DatosTecnicosActivo(
    string? Procesador,
    string? VelocidadGeneracion,
    int? Nucleos,
    string? TarjetaGrafica,
    decimal? PantallaPulgadas,
    Guid? SistemaOperativoId,
    string? VersionSistemaOperativo,
    string? NombreEquipo,
    string? DominioGrupo,
    string? DireccionIp,
    string? DireccionMac,
    string? Antivirus,
    bool? DiscoCifrado,
    string? PuertosPrincipales,
    string? AccesoriosIncluidos)
{
    public static DatosTecnicosActivo Vacios { get; } =
        new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
}

/// <summary>Datos que se calculan a una fecha; no se guardan.</summary>
/// <param name="AntiguedadAnios">Años desde la adquisición, con un decimal.</param>
/// <param name="Garantia">Si la garantía sigue vigente.</param>
/// <param name="VidaUtilRestanteAnios">Años de vida útil que quedan (negativo si ya se agotó).</param>
/// <param name="Renovacion">Situación de renovación.</param>
public sealed record SituacionActivo(
    decimal? AntiguedadAnios,
    EstadoGarantia Garantia,
    decimal? VidaUtilRestanteAnios,
    SituacionRenovacion Renovacion);

/// <summary>
/// Activo TIC: la ficha técnica de un equipo (docs/ARQUITECTURA.md §5.10). Pertenece a una sede y a un establecimiento
/// de esa sede; no cambia de sede. Los discos y módulos de RAM forman parte de él.
/// </summary>
public sealed partial class Activo : EntidadAuditable, IPerteneceASede
{
    public const int LongitudMaximaCodigo = 30;
    public const int LongitudMaximaModelo = 100;
    public const int LongitudMaximaSerie = 60;
    public const int LongitudMaximaDescripcion = 250;
    public const int LongitudMaximaUbicacion = 150;
    public const int LongitudMaximaResponsable = 150;
    public const int LongitudMaximaObservaciones = 1000;
    public const int LongitudMaximaProveedor = 150;
    public const int LongitudMaximaOrdenCompra = 50;
    public const int LongitudMaximaTextoTecnico = 100;
    public const int LongitudMaximaTextoCorto = 50;
    public const int LongitudMaximaTextoLargo = 250;
    public const int LongitudMaximaNombreEquipo = 63;
    public const int LongitudMaximaIp = 45;
    public const int LongitudMac = 17;
    public const decimal ValorMaximo = 99_999_999.99m;
    public const int VidaUtilMaxima = 50;

    private readonly List<DiscoActivo> _discos = [];
    private readonly List<ModuloRam> _modulosRam = [];

    // Requerido por EF Core.
    private Activo()
    {
    }

    private Activo(Guid sedeId)
    {
        SedeId = sedeId;
    }

    public Guid SedeId { get; private set; }

    public Guid EstablecimientoId { get; private set; }

    // Identificación, ubicación y estado.
    public string CodigoPatrimonial { get; private set; } = string.Empty;

    public Guid TipoActivoId { get; private set; }

    public Guid MarcaId { get; private set; }

    public string Modelo { get; private set; } = string.Empty;

    public string? NumeroSerie { get; private set; }

    public string? Descripcion { get; private set; }

    public Guid AreaId { get; private set; }

    public string? UbicacionFisica { get; private set; }

    public string? Responsable { get; private set; }

    public Guid EstadoActivoId { get; private set; }

    public Conservacion Conservacion { get; private set; }

    public Criticidad? Criticidad { get; private set; }

    public string? Observaciones { get; private set; }

    // Adquisición y garantía.
    public DateOnly? FechaAdquisicion { get; private set; }

    public string? Proveedor { get; private set; }

    public string? NumeroOrdenCompra { get; private set; }

    public TipoAdquisicion? TipoAdquisicion { get; private set; }

    public decimal? ValorAdquisicion { get; private set; }

    public int? VidaUtilAnios { get; private set; }

    public DateOnly? FinGarantia { get; private set; }

    /// <summary>
    /// Fecha en que se agota la vida útil (adquisición + vida útil). Se guarda para poder filtrar por situación de
    /// renovación en la base de datos; la calcula la entidad, nunca se asigna desde fuera.
    /// </summary>
    public DateOnly? FechaRenovacion { get; private set; }

    // Características técnicas.
    public string? Procesador { get; private set; }

    public string? VelocidadGeneracion { get; private set; }

    public int? Nucleos { get; private set; }

    public string? TarjetaGrafica { get; private set; }

    public decimal? PantallaPulgadas { get; private set; }

    public Guid? SistemaOperativoId { get; private set; }

    public string? VersionSistemaOperativo { get; private set; }

    public string? NombreEquipo { get; private set; }

    public string? DominioGrupo { get; private set; }

    public string? DireccionIp { get; private set; }

    public string? DireccionMac { get; private set; }

    public string? Antivirus { get; private set; }

    public bool? DiscoCifrado { get; private set; }

    public string? PuertosPrincipales { get; private set; }

    public string? AccesoriosIncluidos { get; private set; }

    public IReadOnlyCollection<DiscoActivo> Discos => _discos.AsReadOnly();

    public IReadOnlyCollection<ModuloRam> ModulosRam => _modulosRam.AsReadOnly();

    /// <summary>Suma de los módulos de RAM, en GB.</summary>
    public int RamTotalGb => _modulosRam.Sum(m => m.CapacidadGb);

    /// <summary>Suma de los discos, en GB.</summary>
    public int AlmacenamientoTotalGb => _discos.Sum(d => d.CapacidadGb);

    public static Activo Crear(
        Guid sedeId,
        Guid establecimientoId,
        DatosGeneralesActivo generales,
        DatosAdquisicionActivo adquisicion,
        DatosTecnicosActivo tecnicos,
        IReadOnlyList<DatosDisco> discos,
        IReadOnlyList<DatosModuloRam> modulosRam)
    {
        if (sedeId == Guid.Empty)
        {
            throw new DominioException("Un activo debe pertenecer a una sede.");
        }

        var activo = new Activo(sedeId);
        activo.Actualizar(establecimientoId, generales, adquisicion, tecnicos, discos, modulosRam);
        return activo;
    }

    /// <summary>Reemplaza toda la ficha, incluidos discos y RAM. La sede no cambia.</summary>
    public void Actualizar(
        Guid establecimientoId,
        DatosGeneralesActivo generales,
        DatosAdquisicionActivo adquisicion,
        DatosTecnicosActivo tecnicos,
        IReadOnlyList<DatosDisco> discos,
        IReadOnlyList<DatosModuloRam> modulosRam)
    {
        ArgumentNullException.ThrowIfNull(generales);
        ArgumentNullException.ThrowIfNull(adquisicion);
        ArgumentNullException.ThrowIfNull(tecnicos);
        ArgumentNullException.ThrowIfNull(discos);
        ArgumentNullException.ThrowIfNull(modulosRam);

        if (establecimientoId == Guid.Empty)
        {
            throw new DominioException("Elija el establecimiento.");
        }

        if (discos.Count > ListasActivo.MaximoDiscos)
        {
            throw new DominioException($"Un activo puede tener hasta {ListasActivo.MaximoDiscos} discos.");
        }

        if (modulosRam.Count > ListasActivo.MaximoModulosRam)
        {
            throw new DominioException($"Un activo puede tener hasta {ListasActivo.MaximoModulosRam} módulos de RAM.");
        }

        // Se valida todo antes de asignar, para no dejar la ficha a medio cambiar si algo es inválido.
        var nuevosDiscos = discos.Select((d, i) => DiscoActivo.Crear(Id, i, d)).ToList();
        var nuevosModulos = modulosRam.Select((m, i) => ModuloRam.Crear(Id, i, m)).ToList();
        var asignar = ValidarGenerales(generales) + ValidarAdquisicion(adquisicion) + ValidarTecnicos(tecnicos);

        EstablecimientoId = establecimientoId;
        asignar.Invoke(this);

        _discos.Clear();
        _discos.AddRange(nuevosDiscos);
        _modulosRam.Clear();
        _modulosRam.AddRange(nuevosModulos);
    }

    /// <summary>Antigüedad, garantía y renovación a la fecha indicada (hoy, en la zona horaria del sistema).</summary>
    public SituacionActivo Situacion(DateOnly hoy)
    {
        decimal? antiguedad = FechaAdquisicion is DateOnly adquirido
            ? Math.Round(Math.Max(0, hoy.DayNumber - adquirido.DayNumber) / 365.25m, 1)
            : null;

        var garantia = GarantiaAl(FinGarantia, hoy);
        decimal? restante = antiguedad is decimal a && VidaUtilAnios is int vida ? vida - a : null;
        var renovacion = RenovacionAl(FechaRenovacion, hoy);

        return new SituacionActivo(antiguedad, garantia, restante, renovacion);
    }

    /// <summary>Estado de la garantía a una fecha, según su fecha de fin.</summary>
    public static EstadoGarantia GarantiaAl(DateOnly? finGarantia, DateOnly hoy) => finGarantia switch
    {
        null => EstadoGarantia.SinDato,
        DateOnly fin when fin >= hoy => EstadoGarantia.Vigente,
        _ => EstadoGarantia.Vencida,
    };

    /// <summary>Situación de renovación a una fecha, según la fecha en que se agota la vida útil.</summary>
    public static SituacionRenovacion RenovacionAl(DateOnly? fechaRenovacion, DateOnly hoy) => fechaRenovacion switch
    {
        null => SituacionRenovacion.SinDato,
        DateOnly fecha when fecha <= hoy => SituacionRenovacion.Renovar,
        DateOnly fecha when fecha <= hoy.AddYears(ListasActivo.AniosAvisoRenovacion) => SituacionRenovacion.ProximoARenovar,
        _ => SituacionRenovacion.Vigente,
    };

    /// <summary>Normaliza el código patrimonial como se guarda (sin espacios a los lados y en mayúsculas).</summary>
    public static string NormalizarCodigo(string? codigo) =>
        Guardas.TextoObligatorio(codigo, "El código patrimonial", LongitudMaximaCodigo).ToUpperInvariant();

    private static Action<Activo> ValidarGenerales(DatosGeneralesActivo g)
    {
        var codigo = NormalizarCodigo(g.CodigoPatrimonial);
        var modelo = Guardas.TextoObligatorio(g.Modelo, "El modelo", LongitudMaximaModelo);
        var serie = Guardas.TextoOpcional(g.NumeroSerie, "El número de serie", LongitudMaximaSerie);
        var descripcion = Guardas.TextoOpcional(g.Descripcion, "La descripción", LongitudMaximaDescripcion);
        var ubicacion = Guardas.TextoOpcional(g.UbicacionFisica, "La ubicación física", LongitudMaximaUbicacion);
        var responsable = Guardas.TextoOpcional(g.Responsable, "El responsable", LongitudMaximaResponsable);
        var observaciones = Guardas.TextoOpcional(g.Observaciones, "Las observaciones", LongitudMaximaObservaciones);

        Exigir(g.TipoActivoId != Guid.Empty, "Elija el tipo de activo.");
        Exigir(g.MarcaId != Guid.Empty, "Elija la marca.");
        Exigir(g.AreaId != Guid.Empty, "Elija el área.");
        Exigir(g.EstadoActivoId != Guid.Empty, "Elija el estado.");
        Exigir(Enum.IsDefined(g.Conservacion), "Elija el estado de conservación.");
        Exigir(!g.Criticidad.HasValue || Enum.IsDefined(g.Criticidad.Value), "La criticidad no es válida.");

        return a =>
        {
            a.CodigoPatrimonial = codigo;
            a.TipoActivoId = g.TipoActivoId;
            a.MarcaId = g.MarcaId;
            a.Modelo = modelo;
            a.NumeroSerie = serie;
            a.Descripcion = descripcion;
            a.AreaId = g.AreaId;
            a.UbicacionFisica = ubicacion;
            a.Responsable = responsable;
            a.EstadoActivoId = g.EstadoActivoId;
            a.Conservacion = g.Conservacion;
            a.Criticidad = g.Criticidad;
            a.Observaciones = observaciones;
        };
    }

    private static Action<Activo> ValidarAdquisicion(DatosAdquisicionActivo d)
    {
        var proveedor = Guardas.TextoOpcional(d.Proveedor, "El proveedor", LongitudMaximaProveedor);
        var orden = Guardas.TextoOpcional(d.NumeroOrdenCompra, "El N° de OC o factura", LongitudMaximaOrdenCompra);

        Exigir(!d.TipoAdquisicion.HasValue || Enum.IsDefined(d.TipoAdquisicion.Value), "El tipo de adquisición no es válido.");
        Exigir(
            d.ValorAdquisicion is not decimal v || (v >= 0 && v <= ValorMaximo && decimal.Round(v, 2) == v),
            "El valor de adquisición debe ser un monto positivo con hasta 2 decimales.");
        Exigir(
            d.VidaUtilAnios is not int vida || (vida >= 1 && vida <= VidaUtilMaxima),
            $"La vida útil debe estar entre 1 y {VidaUtilMaxima} años.");
        Exigir(
            d.FechaAdquisicion is not DateOnly inicio || d.FinGarantia is not DateOnly fin || fin >= inicio,
            "El fin de la garantía no puede ser anterior a la fecha de adquisición.");

        var renovacion = d.FechaAdquisicion is DateOnly fecha && d.VidaUtilAnios is int anios ? fecha.AddYears(anios) : (DateOnly?)null;

        return a =>
        {
            a.FechaAdquisicion = d.FechaAdquisicion;
            a.Proveedor = proveedor;
            a.NumeroOrdenCompra = orden;
            a.TipoAdquisicion = d.TipoAdquisicion;
            a.ValorAdquisicion = d.ValorAdquisicion;
            a.VidaUtilAnios = d.VidaUtilAnios;
            a.FinGarantia = d.FinGarantia;
            a.FechaRenovacion = renovacion;
        };
    }

    private static Action<Activo> ValidarTecnicos(DatosTecnicosActivo t)
    {
        var procesador = Guardas.TextoOpcional(t.Procesador, "El procesador", LongitudMaximaTextoTecnico);
        var velocidad = Guardas.TextoOpcional(t.VelocidadGeneracion, "La velocidad o generación", LongitudMaximaTextoCorto);
        var grafica = Guardas.TextoOpcional(t.TarjetaGrafica, "La tarjeta gráfica", LongitudMaximaTextoTecnico);
        var version = Guardas.TextoOpcional(t.VersionSistemaOperativo, "La versión del sistema operativo", LongitudMaximaTextoCorto);
        var dominio = Guardas.TextoOpcional(t.DominioGrupo, "El dominio o grupo de trabajo", LongitudMaximaTextoTecnico);
        var antivirus = Guardas.TextoOpcional(t.Antivirus, "El antivirus", LongitudMaximaTextoTecnico);
        var puertos = Guardas.TextoOpcional(t.PuertosPrincipales, "Los puertos principales", LongitudMaximaTextoLargo);
        var accesorios = Guardas.TextoOpcional(t.AccesoriosIncluidos, "Los accesorios", LongitudMaximaTextoLargo);
        var nombreEquipo = ValidarNombreEquipo(t.NombreEquipo);
        var ip = ValidarIp(t.DireccionIp);
        var mac = NormalizarMac(t.DireccionMac);

        Exigir(t.Nucleos is not int n || (n >= 1 && n <= 512), "El número de núcleos debe estar entre 1 y 512.");
        Exigir(
            t.PantallaPulgadas is not decimal p || (p >= 1 && p <= 150 && decimal.Round(p, 1) == p),
            "El tamaño de pantalla debe estar entre 1 y 150 pulgadas, con hasta 1 decimal.");

        return a =>
        {
            a.Procesador = procesador;
            a.VelocidadGeneracion = velocidad;
            a.Nucleos = t.Nucleos;
            a.TarjetaGrafica = grafica;
            a.PantallaPulgadas = t.PantallaPulgadas;
            a.SistemaOperativoId = t.SistemaOperativoId == Guid.Empty ? null : t.SistemaOperativoId;
            a.VersionSistemaOperativo = version;
            a.NombreEquipo = nombreEquipo;
            a.DominioGrupo = dominio;
            a.DireccionIp = ip;
            a.DireccionMac = mac;
            a.Antivirus = antivirus;
            a.DiscoCifrado = t.DiscoCifrado;
            a.PuertosPrincipales = puertos;
            a.AccesoriosIncluidos = accesorios;
        };
    }

    private static string? ValidarNombreEquipo(string? nombre)
    {
        var limpio = Guardas.TextoOpcional(nombre, "El nombre de equipo", LongitudMaximaNombreEquipo);
        Exigir(
            limpio is null || PatronNombreEquipo().IsMatch(limpio),
            "El nombre de equipo solo puede tener letras sin tilde, números, puntos y guiones (por ejemplo ADM-LT-001).");
        return limpio;
    }

    private static string? ValidarIp(string? ip)
    {
        var limpio = Guardas.TextoOpcional(ip, "La dirección IP", LongitudMaximaIp);
        if (limpio is null)
        {
            return null;
        }

        // IPv4 con sus cuatro números (IPAddress.TryParse sola aceptaría "10" como 0.0.0.10), o IPv6.
        var valida = limpio.Contains(':', StringComparison.Ordinal)
            ? IPAddress.TryParse(limpio, out var v6) && v6.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            : PatronIpv4().IsMatch(limpio) && limpio.Split('.').All(p => int.Parse(p, CultureInfo.InvariantCulture) <= 255);

        Exigir(valida, "La dirección IP no es válida (por ejemplo 192.168.10.45).");
        return limpio;
    }

    /// <summary>Acepta A4:BB:6D:12:34:56, a4-bb-6d-12-34-56 o A4BB6D123456 y la guarda como A4:BB:6D:12:34:56.</summary>
    public static string? NormalizarMac(string? mac)
    {
        var limpio = Guardas.TextoOpcional(mac, "La dirección MAC", LongitudMac + 5);
        if (limpio is null)
        {
            return null;
        }

        var hexadecimal = limpio.Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        Exigir(PatronMac().IsMatch(hexadecimal), "La dirección MAC no es válida (por ejemplo A4:BB:6D:12:34:56).");
        return string.Join(':', Enumerable.Range(0, 6).Select(i => hexadecimal.Substring(i * 2, 2)));
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion)
        {
            throw new DominioException(mensaje);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.-]*$")]
    private static partial Regex PatronNombreEquipo();

    [GeneratedRegex("^[0-9]{1,3}(\\.[0-9]{1,3}){3}$")]
    private static partial Regex PatronIpv4();

    [GeneratedRegex("^[0-9A-F]{12}$")]
    private static partial Regex PatronMac();
}
