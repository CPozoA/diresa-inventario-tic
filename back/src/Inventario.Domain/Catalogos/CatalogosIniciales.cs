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
}
