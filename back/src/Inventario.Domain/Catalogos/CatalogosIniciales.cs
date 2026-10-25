using System.Globalization;
using System.Text;

namespace Inventario.Domain.Catalogos;

/// <summary>
/// Valores con los que arrancan los catálogos generales. Se cargan una sola vez, cuando el catálogo está vacío;
/// después el superadministrador los modifica desde la aplicación.
/// </summary>
public static class CatalogosIniciales
{
    public static IReadOnlyList<string> TiposActivo { get; } =
    [
        "Laptop", "PC de escritorio", "All-in-one", "Monitor", "Impresora", "Multifuncional", "Escáner", "Proyector",
        "Servidor", "Switch", "Router", "Access point", "Firewall", "UPS", "Teléfono IP", "Tablet", "Disco externo", "Otro",
    ];

    public static IReadOnlyList<string> Marcas { get; } =
    [
        "HP", "Dell", "Lenovo", "Acer", "Asus", "Apple", "Samsung", "LG", "Epson", "Brother", "Canon", "Kyocera",
        "Ricoh", "Cisco", "TP-Link", "Ubiquiti", "MikroTik", "APC", "Forza", "Otra",
    ];

    public static IReadOnlyList<string> EstadosActivo { get; } =
    [
        "Operativo", "En mantenimiento", "Inoperativo", "En almacén", "De baja",
    ];

    public static IReadOnlyList<string> SistemasOperativos { get; } =
    [
        "Windows 11 Pro", "Windows 11 Home", "Windows 10 Pro", "Windows 10 Home", "Windows Server 2022",
        "Windows Server 2019", "Ubuntu", "Debian", "macOS", "Android", "Sin sistema operativo",
    ];

    public static IReadOnlyList<string> Procesadores { get; } =
    [
        "Intel Core i3", "Intel Core i5", "Intel Core i7", "Intel Core i9", "Intel Core Ultra 5", "Intel Core Ultra 7",
        "Intel Core Ultra 9", "Intel Celeron", "Intel Pentium", "Intel Atom", "Intel Xeon", "AMD Ryzen 3", "AMD Ryzen 5",
        "AMD Ryzen 7", "AMD Ryzen 9", "AMD Athlon", "AMD EPYC", "Apple M1", "Apple M2", "Apple M3", "Apple M4",
        "Qualcomm Snapdragon X", "Otro",
    ];

    public static IReadOnlyList<string> Antivirus { get; } =
    [
        "Microsoft Defender", "ESET", "Kaspersky", "Bitdefender", "Sophos", "Trend Micro", "CrowdStrike Falcon",
        "SentinelOne", "McAfee", "Avast", "Ninguno",
    ];

    public static IReadOnlyList<string> TiposLicencia { get; } =
    [
        "Perpetua", "Suscripción anual", "Suscripción mensual", "OEM", "Volumen", "Software libre",
    ];

    private static readonly string[] TiposComputo = ["LAPTOP", "PC DE ESCRITORIO", "ALL IN ONE", "SERVIDOR", "TABLET", "WORKSTATION"];

    private static readonly string[] TiposRed =
    [
        "IMPRESORA", "MULTIFUNCIONAL", "ESCANER", "SWITCH", "ROUTER", "ACCESS POINT", "FIREWALL", "TELEFONO IP",
    ];

    /// <summary>
    /// Ficha técnica que corresponde a un tipo por su nombre (D43): cómputo para laptops, PC, servidores…; red para
    /// impresoras, switches…; ninguna para el resto. El superadministrador la puede cambiar después.
    /// </summary>
    public static FichaTecnica FichaPorOmision(string nombre)
    {
        var clave = Clave(nombre);
        if (TiposComputo.Contains(clave))
        {
            return FichaTecnica.Computo;
        }

        return TiposRed.Contains(clave) ? FichaTecnica.Red : FichaTecnica.Ninguna;
    }

    /// <summary>
    /// Generaciones que corresponden a una familia de procesadores por su nombre (D44): "Intel Core i…", "Intel Core
    /// Ultra…" y "AMD Ryzen…" tienen las suyas; las demás, ninguna. El superadministrador las puede cambiar después.
    /// </summary>
    public static GeneracionesProcesador GeneracionesPorOmision(string nombre)
    {
        var clave = Clave(nombre);
        if (clave.StartsWith("INTEL CORE ULTRA", StringComparison.Ordinal))
        {
            return GeneracionesProcesador.IntelCoreUltra;
        }

        if (clave.StartsWith("INTEL CORE I", StringComparison.Ordinal))
        {
            return GeneracionesProcesador.IntelCore;
        }

        return clave.StartsWith("AMD RYZEN", StringComparison.Ordinal) ? GeneracionesProcesador.AmdRyzen : GeneracionesProcesador.Ninguna;
    }

    /// <summary>Mayúsculas, sin tildes y con un espacio entre palabras ("All-in-one" → "ALL IN ONE").</summary>
    private static string Clave(string texto)
    {
        var resultado = new StringBuilder();
        var espacio = false;
        foreach (var caracter in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caracter))
            {
                if (espacio && resultado.Length > 0)
                {
                    resultado.Append(' ');
                }

                resultado.Append(char.ToUpperInvariant(caracter));
                espacio = false;
            }
            else
            {
                espacio = true;
            }
        }

        return resultado.ToString();
    }
}
