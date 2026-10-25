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
