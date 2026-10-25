using Inventario.Domain.Common;

namespace Inventario.Domain.Activos;

/// <summary>Datos de un disco tal como se registran en la ficha.</summary>
/// <param name="Ranura">Dónde está instalado (M.2 #1, SATA 0…). Opcional.</param>
/// <param name="Tipo">Tecnología.</param>
/// <param name="CapacidadGb">Capacidad en GB.</param>
/// <param name="MarcaModelo">Marca y modelo, por ejemplo "Samsung PM9B1".</param>
/// <param name="NumeroSerie">Número de serie del disco.</param>
/// <param name="Uso">Para qué se usa.</param>
public sealed record DatosDisco(string? Ranura, TipoDisco Tipo, int CapacidadGb, string? MarcaModelo, string? NumeroSerie, UsoDisco Uso);

/// <summary>Datos de un módulo de memoria RAM.</summary>
/// <param name="Ranura">Ranura (A, B, DIMM1…). Opcional.</param>
/// <param name="Tipo">Tipo de memoria.</param>
/// <param name="CapacidadGb">Capacidad en GB (de la lista fija).</param>
/// <param name="VelocidadMhz">Velocidad en MHz (de la lista de su tipo). Opcional.</param>
/// <param name="MarcaModelo">Marca y modelo.</param>
public sealed record DatosModuloRam(string? Ranura, TipoRam Tipo, int CapacidadGb, int? VelocidadMhz, string? MarcaModelo);

/// <summary>Disco instalado en un activo. Forma parte del activo: se registra y se modifica a través de él.</summary>
public sealed class DiscoActivo : Entidad
{
    public const int LongitudMaximaRanura = 20;
    public const int LongitudMaximaMarcaModelo = 100;
    public const int LongitudMaximaSerie = 60;

    // Requerido por EF Core.
    private DiscoActivo()
    {
    }

    private DiscoActivo(Guid activoId, int orden, DatosDisco datos)
    {
        if (!Enum.IsDefined(datos.Tipo))
        {
            throw new DominioException("Elija el tipo de disco.");
        }

        if (!Enum.IsDefined(datos.Uso))
        {
            throw new DominioException("Elija el uso del disco.");
        }

        if (datos.CapacidadGb <= 0 || datos.CapacidadGb > ListasActivo.CapacidadMaximaDiscoGb)
        {
            throw new DominioException(
                $"La capacidad del disco debe estar entre 1 y {ListasActivo.CapacidadMaximaDiscoGb} GB.");
        }

        ActivoId = activoId;
        Orden = orden;
        Ranura = Guardas.TextoOpcional(datos.Ranura, "La ranura del disco", LongitudMaximaRanura);
        Tipo = datos.Tipo;
        CapacidadGb = datos.CapacidadGb;
        MarcaModelo = Guardas.TextoOpcional(datos.MarcaModelo, "La marca y modelo del disco", LongitudMaximaMarcaModelo);
        NumeroSerie = Guardas.TextoOpcional(datos.NumeroSerie, "El número de serie del disco", LongitudMaximaSerie);
        Uso = datos.Uso;
    }

    public Guid ActivoId { get; private set; }

    /// <summary>Posición en la ficha (0, 1, 2…), para mostrarlos en el orden en que se registraron.</summary>
    public int Orden { get; private set; }

    public string? Ranura { get; private set; }

    public TipoDisco Tipo { get; private set; }

    public int CapacidadGb { get; private set; }

    public string? MarcaModelo { get; private set; }

    public string? NumeroSerie { get; private set; }

    public UsoDisco Uso { get; private set; }

    internal static DiscoActivo Crear(Guid activoId, int orden, DatosDisco datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return new DiscoActivo(activoId, orden, datos);
    }
}

/// <summary>Módulo de memoria RAM instalado en un activo. Forma parte del activo.</summary>
public sealed class ModuloRam : Entidad
{
    public const int LongitudMaximaRanura = 20;
    public const int LongitudMaximaMarcaModelo = 100;

    // Requerido por EF Core.
    private ModuloRam()
    {
    }

    private ModuloRam(Guid activoId, int orden, DatosModuloRam datos)
    {
        if (!Enum.IsDefined(datos.Tipo))
        {
            throw new DominioException("Elija el tipo de memoria RAM.");
        }

        if (!ListasActivo.CapacidadesRamGb.Contains(datos.CapacidadGb))
        {
            throw new DominioException(
                $"La capacidad del módulo de RAM debe ser una de: {string.Join(", ", ListasActivo.CapacidadesRamGb)} GB.");
        }

        if (datos.VelocidadMhz is int velocidad && !ListasActivo.VelocidadesRamMhz[datos.Tipo].Contains(velocidad))
        {
            throw new DominioException(
                $"La velocidad {velocidad} MHz no corresponde a {ListasActivo.Texto(datos.Tipo)}. Use una de: "
                + $"{string.Join(", ", ListasActivo.VelocidadesRamMhz[datos.Tipo])} MHz.");
        }

        ActivoId = activoId;
        Orden = orden;
        Ranura = Guardas.TextoOpcional(datos.Ranura, "La ranura de la RAM", LongitudMaximaRanura);
        Tipo = datos.Tipo;
        CapacidadGb = datos.CapacidadGb;
        VelocidadMhz = datos.VelocidadMhz;
        MarcaModelo = Guardas.TextoOpcional(datos.MarcaModelo, "La marca y modelo de la RAM", LongitudMaximaMarcaModelo);
    }

    public Guid ActivoId { get; private set; }

    /// <summary>Posición en la ficha.</summary>
    public int Orden { get; private set; }

    public string? Ranura { get; private set; }

    public TipoRam Tipo { get; private set; }

    public int CapacidadGb { get; private set; }

    public int? VelocidadMhz { get; private set; }

    public string? MarcaModelo { get; private set; }

    internal static ModuloRam Crear(Guid activoId, int orden, DatosModuloRam datos)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return new ModuloRam(activoId, orden, datos);
    }
}
