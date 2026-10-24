using System.Text.RegularExpressions;
using Inventario.Domain.Common;

namespace Inventario.Domain.Establecimientos;

/// <summary>Datos de un establecimiento tal como llegan de la lista oficial (formulario o Excel).</summary>
/// <param name="IdEstablecimiento">Identificador de la lista oficial (no es la clave interna).</param>
/// <param name="CodigoUnico">Código único del establecimiento.</param>
/// <param name="Nombre">Nombre del establecimiento.</param>
/// <param name="Categoria">Categoría, por ejemplo <c>II-1</c>.</param>
/// <param name="Microred">Microred a la que pertenece.</param>
/// <param name="Provincia">Provincia.</param>
/// <param name="Distrito">Distrito.</param>
/// <param name="Ubigeo">Código de ubigeo de 6 dígitos, por ejemplo <c>050601</c>.</param>
public sealed record DatosEstablecimiento(
    int IdEstablecimiento,
    int CodigoUnico,
    string Nombre,
    string? Categoria,
    string? Microred,
    string? Provincia,
    string? Distrito,
    string? Ubigeo);

/// <summary>
/// Establecimiento de salud (puesto, centro u hospital) de una red. Cada activo pertenece a uno
/// (docs/ARQUITECTURA.md D39, §5.9). Solo el superadministrador los registra, corrige y desactiva.
/// No se eliminan: un establecimiento desactivado deja de ofrecerse al registrar activos.
/// </summary>
public sealed partial class Establecimiento : EntidadAuditable, IPerteneceASede
{
    public const int LongitudMaximaNombre = 200;
    public const int LongitudMaximaCategoria = 20;
    public const int LongitudMaximaMicrored = 150;
    public const int LongitudMaximaProvincia = 100;
    public const int LongitudMaximaDistrito = 100;
    public const int LongitudUbigeo = 6;

    // Requerido por EF Core.
    private Establecimiento()
    {
    }

    private Establecimiento(Guid sedeId)
    {
        SedeId = sedeId;
        Activo = true;
    }

    /// <summary>Red (sede) a la que pertenece. No cambia: no existen transferencias entre sedes.</summary>
    public Guid SedeId { get; private set; }

    /// <summary>Identificador de la lista oficial. Único en todo el sistema; con él se reconoce al reimportar.</summary>
    public int IdEstablecimiento { get; private set; }

    /// <summary>Código único del establecimiento. Único en todo el sistema; con él se busca al registrar activos.</summary>
    public int CodigoUnico { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string? Categoria { get; private set; }

    public string? Microred { get; private set; }

    public string? Provincia { get; private set; }

    public string? Distrito { get; private set; }

    public string? Ubigeo { get; private set; }

    public bool Activo { get; private set; }

    public static Establecimiento Crear(Guid sedeId, DatosEstablecimiento datos)
    {
        if (sedeId == Guid.Empty)
        {
            throw new DominioException("Indique la red (sede) del establecimiento.");
        }

        var establecimiento = new Establecimiento(sedeId);
        establecimiento.Actualizar(datos);
        return establecimiento;
    }

    /// <summary>Reemplaza los datos (la sede no cambia). Devuelve verdadero si algo cambió.</summary>
    public bool Actualizar(DatosEstablecimiento datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // Primero se valida todo; solo después se asigna, para no dejar la entidad a medio cambiar.
        var limpio = Validar(datos);
        if (limpio == DatosActuales())
        {
            return false;
        }

        IdEstablecimiento = limpio.IdEstablecimiento;
        CodigoUnico = limpio.CodigoUnico;
        Nombre = limpio.Nombre;
        Categoria = limpio.Categoria;
        Microred = limpio.Microred;
        Provincia = limpio.Provincia;
        Distrito = limpio.Distrito;
        Ubigeo = limpio.Ubigeo;
        return true;
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    private DatosEstablecimiento DatosActuales() =>
        new(IdEstablecimiento, CodigoUnico, Nombre, Categoria, Microred, Provincia, Distrito, Ubigeo);

    private static DatosEstablecimiento Validar(DatosEstablecimiento datos)
    {
        if (datos.IdEstablecimiento <= 0)
        {
            throw new DominioException("El Id_Establecimiento debe ser un número mayor que cero.");
        }

        if (datos.CodigoUnico <= 0)
        {
            throw new DominioException("El código único debe ser un número mayor que cero.");
        }

        var ubigeo = Guardas.TextoOpcional(datos.Ubigeo, "El ubigeo", LongitudUbigeo + 10);
        if (ubigeo is not null && !PatronUbigeo().IsMatch(ubigeo))
        {
            throw new DominioException("El ubigeo debe tener 6 dígitos, por ejemplo 050601.");
        }

        return new DatosEstablecimiento(
            datos.IdEstablecimiento,
            datos.CodigoUnico,
            Guardas.TextoObligatorio(datos.Nombre, "El nombre del establecimiento", LongitudMaximaNombre),
            Guardas.TextoOpcional(datos.Categoria, "La categoría", LongitudMaximaCategoria),
            Guardas.TextoOpcional(datos.Microred, "La microred", LongitudMaximaMicrored),
            Guardas.TextoOpcional(datos.Provincia, "La provincia", LongitudMaximaProvincia),
            Guardas.TextoOpcional(datos.Distrito, "El distrito", LongitudMaximaDistrito),
            ubigeo);
    }

    [GeneratedRegex("^[0-9]{6}$")]
    private static partial Regex PatronUbigeo();
}
