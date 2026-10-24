using System.Globalization;
using System.Text;
using Inventario.Domain.Sedes;

namespace Inventario.Domain.Establecimientos;

/// <summary>
/// Reconoce a qué sede corresponde el texto de la columna "Red" de la lista de establecimientos.
/// Acepta variantes habituales: "Red de Salud Huanta", "RED HUANTA", "Huanta", "HUANTA", "San Francisco" o "SAN-FRANCISCO".
/// No distingue mayúsculas, tildes ni signos.
/// </summary>
public static class ReconocedorRed
{
    private static readonly string[] Prefijos = ["RED DE SALUD ", "RED SALUD ", "RED "];

    /// <summary>
    /// Sede que corresponde al texto. Primero busca una coincidencia exacta con el código o el nombre de la sede;
    /// si no la hay, acepta el texto si contiene el nombre de una sola sede (por ejemplo, "Ayacucho Centro" → Centro).
    /// Devuelve <c>null</c> si no reconoce ninguna o si el texto es ambiguo.
    /// </summary>
    public static Sede? Buscar(string? red, IEnumerable<Sede> sedes)
    {
        ArgumentNullException.ThrowIfNull(sedes);

        var clave = Normalizar(red);
        if (clave.Length == 0)
        {
            return null;
        }

        var candidatas = sedes
            .Select(s => (Sede: s, Claves: new[] { Normalizar(s.Codigo), Normalizar(s.Nombre) }))
            .ToList();

        var exactas = candidatas.Where(c => c.Claves.Contains(clave)).Select(c => c.Sede).Distinct().ToList();
        if (exactas.Count == 1)
        {
            return exactas[0];
        }

        var conPalabras = $" {clave} ";
        var contenidas = candidatas
            .Where(c => c.Claves.Any(k => k.Length > 0 && conPalabras.Contains($" {k} ", StringComparison.Ordinal)))
            .Select(c => c.Sede)
            .Distinct()
            .ToList();

        return contenidas.Count == 1 ? contenidas[0] : null;
    }

    /// <summary>Mayúsculas, sin tildes, solo letras y números separados por un espacio, y sin el prefijo "Red de Salud".</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        var espacioPendiente = false;

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caracter))
            {
                if (espacioPendiente && resultado.Length > 0)
                {
                    resultado.Append(' ');
                }

                resultado.Append(char.ToUpperInvariant(caracter));
                espacioPendiente = false;
            }
            else
            {
                espacioPendiente = true;
            }
        }

        var normalizado = resultado.ToString();
        foreach (var prefijo in Prefijos)
        {
            if (normalizado.StartsWith(prefijo, StringComparison.Ordinal))
            {
                return normalizado[prefijo.Length..];
            }
        }

        return normalizado;
    }
}
