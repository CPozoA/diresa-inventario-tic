namespace Inventario.Application.Common.Seguridad;

/// <summary>Construye enlaces hacia pantallas del frontend (por ejemplo, para correos).</summary>
public interface IEnlacesAplicacion
{
    Uri RestablecerContrasena(Guid usuarioId, string token);
}
