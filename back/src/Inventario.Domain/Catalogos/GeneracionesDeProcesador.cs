using System.Globalization;

namespace Inventario.Domain.Catalogos;

/// <summary>Una generación de procesador: el código que se guarda ("13") y el texto que se muestra ("13.ª generación").</summary>
public sealed record GeneracionProcesador(string Codigo, string Texto);

/// <summary>
/// Generaciones que se ofrecen según la familia del procesador (D44). Se eligen de una lista para que "13.ª",
/// "13", "treceava" no terminen siendo tres valores distintos.
/// </summary>
public static class GeneracionesDeProcesador
{
    public const int LongitudMaximaCodigo = 10;

    private static readonly Dictionary<GeneracionesProcesador, IReadOnlyList<GeneracionProcesador>> Listas =
        new Dictionary<GeneracionesProcesador, IReadOnlyList<GeneracionProcesador>>
        {
            [GeneracionesProcesador.Ninguna] = [],
            [GeneracionesProcesador.IntelCore] = Enumerable.Range(1, 14).Reverse()
                .Select(n => new GeneracionProcesador(Texto(n), $"{Texto(n)}.ª generación"))
                .ToList(),
            [GeneracionesProcesador.IntelCoreUltra] = Enumerable.Range(1, 3).Reverse()
                .Select(n => new GeneracionProcesador(Texto(n), $"Serie {Texto(n)}"))
                .ToList(),
            [GeneracionesProcesador.AmdRyzen] = Enumerable.Range(1, 9).Reverse()
                .Select(n => new GeneracionProcesador(Texto(n * 1000), $"Serie {Texto(n * 1000)}"))
                .ToList(),
        };

    /// <summary>Generaciones de una familia, de la más reciente a la más antigua. Vacía si la familia no tiene.</summary>
    public static IReadOnlyList<GeneracionProcesador> De(GeneracionesProcesador familia) =>
        Listas.TryGetValue(familia, out var lista) ? lista : [];

    /// <summary>La generación con ese código en la familia, o nula si no le corresponde.</summary>
    public static GeneracionProcesador? Buscar(GeneracionesProcesador familia, string? codigo) =>
        codigo is null ? null : De(familia).FirstOrDefault(g => g.Codigo == codigo);

    private static string Texto(int numero) => numero.ToString(CultureInfo.InvariantCulture);
}
