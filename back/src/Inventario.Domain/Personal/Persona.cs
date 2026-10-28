using System.Text.RegularExpressions;
using Inventario.Domain.Common;

namespace Inventario.Domain.Personal;

/// <summary>Nombres y apellidos de una persona, como los da RENIEC o como se escriben a mano.</summary>
/// <param name="ApellidoPaterno">Apellido paterno.</param>
/// <param name="ApellidoMaterno">Apellido materno (puede faltar).</param>
/// <param name="Nombres">Nombres.</param>
public sealed record NombresPersona(string ApellidoPaterno, string? ApellidoMaterno, string Nombres);

/// <summary>Dónde trabaja la persona dentro de la red.</summary>
/// <param name="EstablecimientoId">Establecimiento de la red.</param>
/// <param name="AreaId">Área de la red.</param>
/// <param name="Cargo">Cargo (texto libre, opcional).</param>
public sealed record DatosLaboralesPersona(Guid EstablecimientoId, Guid AreaId, string? Cargo);

/// <summary>
/// Trabajador de una red a quien se le asignan equipos (docs/ARQUITECTURA.md D50, §5.12). Se identifica por su DNI,
/// que no se repite en la red. Los nombres vienen de RENIEC; si RENIEC no encuentra el DNI o no responde, se escriben
/// a mano y la persona queda "sin verificar" hasta confirmarla. Solo se guardan DNI, nombres y apellidos: ni la fecha
/// de nacimiento ni otros datos de RENIEC. Los nombres se guardan en mayúsculas.
/// No se elimina: cuando deja de trabajar en la red se marca como cesada.
/// </summary>
public sealed partial class Persona : EntidadAuditable, IPerteneceASede
{
    public const int LongitudDni = 8;
    public const int LongitudMaximaApellido = 60;
    public const int LongitudMaximaNombres = 100;
    public const int LongitudMaximaNombreCompleto = 230;
    public const int LongitudMaximaCargo = 100;

    // Requerido por EF Core.
    private Persona()
    {
    }

    private Persona(Guid sedeId, string dni)
    {
        if (sedeId == Guid.Empty)
        {
            throw new DominioException("Una persona debe pertenecer a una red.");
        }

        SedeId = sedeId;
        Dni = NormalizarDni(dni);
        Activa = true;
    }

    public Guid SedeId { get; private set; }

    /// <summary>8 dígitos. Único dentro de la red.</summary>
    public string Dni { get; private set; } = string.Empty;

    public string ApellidoPaterno { get; private set; } = string.Empty;

    public string? ApellidoMaterno { get; private set; }

    public string Nombres { get; private set; } = string.Empty;

    /// <summary>"NOMBRES APELLIDO PATERNO APELLIDO MATERNO". Se guarda para buscar por nombre completo.</summary>
    public string NombreCompleto { get; private set; } = string.Empty;

    public Guid EstablecimientoId { get; private set; }

    public Guid AreaId { get; private set; }

    public string? Cargo { get; private set; }

    /// <summary>Cuándo se confirmaron los nombres con RENIEC; <c>null</c> si se escribieron a mano.</summary>
    public DateTimeOffset? VerificadaEn { get; private set; }

    public bool Verificada => VerificadaEn.HasValue;

    /// <summary>Falso cuando la persona cesó: ya no se le asignan equipos ni aparece al buscar.</summary>
    public bool Activa { get; private set; }

    /// <summary>Registra a una persona con los nombres que dio RENIEC.</summary>
    public static Persona RegistrarVerificada(
        Guid sedeId, string dni, NombresPersona deReniec, DatosLaboralesPersona laborales, DateTimeOffset ahora)
    {
        var persona = new Persona(sedeId, dni);
        persona.AsignarNombres(deReniec);
        persona.CambiarDatosLaborales(laborales);
        persona.VerificadaEn = ahora;
        return persona;
    }

    /// <summary>Registra a una persona con nombres escritos a mano (RENIEC no la encontró o no respondió).</summary>
    public static Persona RegistrarSinVerificar(Guid sedeId, string dni, NombresPersona escritos, DatosLaboralesPersona laborales)
    {
        var persona = new Persona(sedeId, dni);
        persona.AsignarNombres(escritos);
        persona.CambiarDatosLaborales(laborales);
        return persona;
    }

    public void CambiarDatosLaborales(DatosLaboralesPersona laborales)
    {
        ArgumentNullException.ThrowIfNull(laborales);

        if (laborales.EstablecimientoId == Guid.Empty)
        {
            throw new DominioException("Elija el establecimiento.");
        }

        if (laborales.AreaId == Guid.Empty)
        {
            throw new DominioException("Elija el área.");
        }

        var cargo = Guardas.TextoOpcional(laborales.Cargo, "El cargo", LongitudMaximaCargo);

        EstablecimientoId = laborales.EstablecimientoId;
        AreaId = laborales.AreaId;
        Cargo = cargo is null ? null : EspaciosRepetidos().Replace(cargo, " ");
    }

    /// <summary>Corrige los nombres escritos a mano. Los de una persona verificada no se cambian: vienen de RENIEC.</summary>
    public void CorregirNombres(NombresPersona escritos)
    {
        if (Verificada)
        {
            throw new DominioException("Los nombres de una persona verificada con RENIEC no se cambian a mano.");
        }

        AsignarNombres(escritos);
    }

    /// <summary>Confirma la persona con los datos de RENIEC (reemplaza los nombres escritos a mano).</summary>
    public void Verificar(NombresPersona deReniec, DateTimeOffset ahora)
    {
        AsignarNombres(deReniec);
        VerificadaEn = ahora;
    }

    /// <summary>
    /// Marca a la persona como cesada. Quien llama comprueba antes que no tenga equipos a su cargo
    /// (las asignaciones vigentes están en otra entidad).
    /// </summary>
    public void Cesar() => Activa = false;

    public void Reactivar() => Activa = true;

    /// <summary>Quita espacios y valida que sean 8 dígitos.</summary>
    public static string NormalizarDni(string? dni)
    {
        var limpio = dni?.Trim() ?? string.Empty;

        if (!EsDniValido(limpio))
        {
            throw new DominioException("El DNI debe tener 8 dígitos.");
        }

        return limpio;
    }

    public static bool EsDniValido(string? dni) => dni is not null && FormatoDni().IsMatch(dni);

    /// <summary>Mayúsculas, sin espacios a los lados ni repetidos: así se guardan y se buscan los nombres.</summary>
    public static string NormalizarNombre(string texto) =>
        EspaciosRepetidos().Replace(texto.Trim(), " ").ToUpperInvariant();

    /// <summary>Nombre completo en el orden de RENIEC: nombres, apellido paterno y apellido materno.</summary>
    public static string ArmarNombreCompleto(string nombres, string apellidoPaterno, string? apellidoMaterno) =>
        apellidoMaterno is null ? $"{nombres} {apellidoPaterno}" : $"{nombres} {apellidoPaterno} {apellidoMaterno}";

    private void AsignarNombres(NombresPersona n)
    {
        ArgumentNullException.ThrowIfNull(n);

        var paterno = Nombre(Guardas.TextoObligatorio(n.ApellidoPaterno, "El apellido paterno", LongitudMaximaApellido), "El apellido paterno");
        var materno = Guardas.TextoOpcional(n.ApellidoMaterno, "El apellido materno", LongitudMaximaApellido) is string m
            ? Nombre(m, "El apellido materno")
            : null;
        var nombres = Nombre(Guardas.TextoObligatorio(n.Nombres, "Los nombres", LongitudMaximaNombres), "Los nombres");

        ApellidoPaterno = paterno;
        ApellidoMaterno = materno;
        Nombres = nombres;
        NombreCompleto = ArmarNombreCompleto(nombres, paterno, materno);
    }

    private static string Nombre(string texto, string campo)
    {
        var normalizado = NormalizarNombre(texto);

        if (!SoloLetras().IsMatch(normalizado))
        {
            throw new DominioException($"{campo} solo puede tener letras, espacios, apóstrofos, puntos y guiones.");
        }

        return normalizado;
    }

    [GeneratedRegex(@"^\d{8}$")]
    private static partial Regex FormatoDni();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspaciosRepetidos();

    [GeneratedRegex(@"^\p{L}[\p{L}\p{M}' .\-]*$")]
    private static partial Regex SoloLetras();
}
