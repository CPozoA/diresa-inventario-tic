using System.Security.Cryptography;

namespace Inventario.Application.Common.Seguridad;

/// <summary>
/// Genera contraseñas temporales aleatorias (criptográficamente seguras) que cumplen la política:
/// 14 caracteres, con mayúscula, minúscula y número, sin caracteres que se confunden (0/O, 1/l/I).
/// </summary>
internal static class GeneradorContrasenaTemporal
{
    private const string Mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Minusculas = "abcdefghijkmnpqrstuvwxyz";
    private const string Digitos = "23456789";
    private const string Todos = Mayusculas + Minusculas + Digitos;
    private const int Longitud = 14;

    public static string Generar()
    {
        while (true)
        {
            var caracteres = new char[Longitud];
            caracteres[0] = Elegir(Mayusculas);
            caracteres[1] = Elegir(Minusculas);
            caracteres[2] = Elegir(Digitos);

            for (var i = 3; i < Longitud; i++)
            {
                caracteres[i] = Elegir(Todos);
            }

            RandomNumberGenerator.Shuffle(caracteres.AsSpan());

            // La política exige al menos 4 caracteres distintos; con 14 al azar casi siempre se cumple.
            if (caracteres.Distinct().Count() >= 4)
            {
                return new string(caracteres);
            }
        }
    }

    private static char Elegir(string fuente) => fuente[RandomNumberGenerator.GetInt32(fuente.Length)];
}
