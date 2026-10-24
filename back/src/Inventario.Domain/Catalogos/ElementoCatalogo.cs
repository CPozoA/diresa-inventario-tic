using Inventario.Domain.Common;

namespace Inventario.Domain.Catalogos;

/// <summary>
/// Elemento de una lista de valores (catálogo): un tipo de activo, una marca, un área… Todos tienen un nombre
/// y pueden desactivarse. Un elemento desactivado conserva su historial pero ya no se ofrece al registrar.
/// No se eliminan: los activos y licencias los referenciarán.
/// </summary>
public abstract class ElementoCatalogo : EntidadAuditable
{
    public const int LongitudMaximaNombre = 100;

    // Requerido por EF Core.
    protected ElementoCatalogo()
    {
    }

    protected ElementoCatalogo(string nombre)
    {
        Nombre = ValidarNombre(nombre);
        Activo = true;
    }

    public string Nombre { get; private set; } = string.Empty;

    public bool Activo { get; private set; }

    public void CambiarNombre(string nombre) => Nombre = ValidarNombre(nombre);

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    private static string ValidarNombre(string nombre) =>
        Guardas.TextoObligatorio(nombre, "El nombre", LongitudMaximaNombre);
}

/// <summary>Catálogo propio de cada sede (áreas). Lo administra la sede.</summary>
public abstract class ElementoCatalogoDeSede : ElementoCatalogo, IPerteneceASede
{
    // Requerido por EF Core.
    protected ElementoCatalogoDeSede()
    {
    }

    protected ElementoCatalogoDeSede(Guid sedeId, string nombre)
        : base(nombre)
    {
        if (sedeId == Guid.Empty)
        {
            throw new DominioException("Un catálogo de sede debe indicar su sede.");
        }

        SedeId = sedeId;
    }

    public Guid SedeId { get; private set; }
}
