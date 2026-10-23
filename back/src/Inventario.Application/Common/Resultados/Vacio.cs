namespace Inventario.Application.Common.Resultados;

/// <summary>
/// "Sin valor": lo devuelven los comandos que solo indican si salieron bien o mal
/// (por ejemplo, cerrar sesión). La Api lo traduce a HTTP 204 No Content.
/// </summary>
public readonly record struct Vacio
{
    public static Vacio Valor => default;
}
