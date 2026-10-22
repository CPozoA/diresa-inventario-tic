using System.Text.RegularExpressions;
using Inventario.Domain.Common;

namespace Inventario.Domain.Sedes;

/// <summary>
/// Una red de salud que registra su propio inventario (por ejemplo, "Red de Salud Huanta").
/// Solo el superadministrador crea, renombra o desactiva sedes.
/// </summary>
public sealed partial class Sede : EntidadAuditable
{
    public const int LongitudMaximaCodigo = 20;
    public const int LongitudMaximaNombre = 150;

    // Requerido por EF Core para materializar la entidad.
    private Sede()
    {
    }

    private Sede(Guid id, string codigo, string nombre)
        : base(id)
    {
        Codigo = codigo;
        Nombre = nombre;
        Activa = true;
    }

    /// <summary>Código corto y único, en mayúsculas (por ejemplo, <c>HUANTA</c>, <c>SAN-MIGUEL</c>).</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Una sede inactiva conserva sus datos, pero sus usuarios no pueden ingresar.</summary>
    public bool Activa { get; private set; }

    public static Sede Crear(string codigo, string nombre) =>
        new(Guid.CreateVersion7(), NormalizarCodigo(codigo), ValidarNombre(nombre));

    /// <summary>Crea una sede con identificador conocido. Solo para los datos iniciales del sistema.</summary>
    public static Sede CrearConId(Guid id, string codigo, string nombre) =>
        new(id, NormalizarCodigo(codigo), ValidarNombre(nombre));

    public void CambiarNombre(string nombre) => Nombre = ValidarNombre(nombre);

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    private static string NormalizarCodigo(string codigo)
    {
        var normalizado = Guardas.TextoObligatorio(codigo, "El código de la sede", LongitudMaximaCodigo)
            .ToUpperInvariant();

        if (!PatronCodigo().IsMatch(normalizado))
        {
            throw new DominioException(
                "El código de la sede solo puede tener letras sin tilde, números y guiones (de 2 a 20 caracteres).");
        }

        return normalizado;
    }

    private static string ValidarNombre(string nombre) =>
        Guardas.TextoObligatorio(nombre, "El nombre de la sede", LongitudMaximaNombre);

    [GeneratedRegex("^[A-Z0-9-]{2,20}$")]
    private static partial Regex PatronCodigo();
}
