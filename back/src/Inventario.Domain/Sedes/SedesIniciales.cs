namespace Inventario.Domain.Sedes;

/// <summary>
/// Las 7 redes de salud con las que arranca el sistema (docs/ARQUITECTURA.md §1.1, D18).
/// Los identificadores son fijos para que los datos iniciales sean idempotentes entre instalaciones.
/// </summary>
public static class SedesIniciales
{
    public static IReadOnlyList<Sede> Crear() =>
    [
        Sede.CrearConId(new Guid("019a0b6e-0000-7000-8000-000000000001"), "HUANTA", "Red de Salud Huanta"),
        Sede.CrearConId(new Guid("019a0b6e-0000-7000-8000-000000000002"), "HUAMANGA", "Red de Salud Huamanga"),
        Sede.CrearConId(new Guid("019a0b6e-0000-7000-8000-000000000003"), "CENTRO", "Red de Salud Centro"),
        Sede.CrearConId(new Guid("019a0b6e-0000-7000-8000-000000000004"), "SAN-FRANCISCO", "Red de Salud San Francisco"),
        Sede.CrearConId(new Guid("019a0b6e-0000-7000-8000-000000000005"), "SAN-MIGUEL", "Red de Salud San Miguel"),
        Sede.CrearConId(new Guid("019a0b6e-0000-7000-8000-000000000006"), "PUQUIO", "Red de Salud Puquio"),
        Sede.CrearConId(new Guid("019a0b6e-0000-7000-8000-000000000007"), "CORACORA", "Red de Salud Coracora"),
    ];
}
