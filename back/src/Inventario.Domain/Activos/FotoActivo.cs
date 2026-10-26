using Inventario.Domain.Common;

namespace Inventario.Domain.Activos;

/// <summary>Formatos de imagen que se aceptan (se reconocen por su contenido, no por el nombre del archivo).</summary>
public enum TipoImagen
{
    Jpeg,
    Png,
    Webp,
}

/// <summary>
/// Foto de un activo (docs/ARQUITECTURA.md D38, §5.8). El archivo está en el almacén de archivos del servidor, fuera
/// de la web pública; aquí solo sus datos. Cada foto tiene además una miniatura (para listas y tarjetas). Una de las
/// fotos del activo es la principal.
/// </summary>
public sealed class FotoActivo : EntidadAuditable, IPerteneceASede
{
    public const int MaximoPorActivo = 6;
    public const int LongitudMaximaDescripcion = 60;

    /// <summary>La interfaz reduce las fotos a 1600 px (unos 300 KB); este límite deja margen y frena abusos.</summary>
    public const int TamanoMaximoBytes = 4 * 1024 * 1024;

    public const int TamanoMaximoMiniaturaBytes = 512 * 1024;

    // Requerido por EF Core.
    private FotoActivo()
    {
    }

    private FotoActivo(Guid sedeId, Guid activoId, int orden, string? descripcion, TipoImagen tipo, int tamano, TipoImagen tipoMiniatura, int tamanoMiniatura)
    {
        if (sedeId == Guid.Empty || activoId == Guid.Empty)
        {
            throw new DominioException("La foto debe pertenecer a un activo.");
        }

        if (tamano <= 0 || tamano > TamanoMaximoBytes || tamanoMiniatura <= 0 || tamanoMiniatura > TamanoMaximoMiniaturaBytes)
        {
            throw new DominioException("El tamaño de la foto no es válido.");
        }

        SedeId = sedeId;
        ActivoId = activoId;
        Orden = orden;
        Descripcion = Guardas.TextoOpcional(descripcion, "La descripción de la foto", LongitudMaximaDescripcion);
        Tipo = tipo;
        TamanoBytes = tamano;
        TipoMiniatura = tipoMiniatura;
        TamanoMiniaturaBytes = tamanoMiniatura;
    }

    public Guid SedeId { get; private set; }

    public Guid ActivoId { get; private set; }

    /// <summary>Posición entre las fotos del activo (0, 1, 2…), en el orden en que se subieron.</summary>
    public int Orden { get; private set; }

    /// <summary>"Placa patrimonial", "Etiqueta de serie"… Opcional.</summary>
    public string? Descripcion { get; private set; }

    public bool Principal { get; private set; }

    public TipoImagen Tipo { get; private set; }

    public int TamanoBytes { get; private set; }

    public TipoImagen TipoMiniatura { get; private set; }

    public int TamanoMiniaturaBytes { get; private set; }

    /// <summary>Dónde está el archivo en el almacén: una carpeta por sede y por activo.</summary>
    public string Clave => $"{SedeId:N}/{ActivoId:N}/{Id:N}{Extension(Tipo)}";

    public string ClaveMiniatura => $"{SedeId:N}/{ActivoId:N}/{Id:N}-mini{Extension(TipoMiniatura)}";

    public static FotoActivo Crear(
        Guid sedeId, Guid activoId, int orden, string? descripcion, TipoImagen tipo, int tamano, TipoImagen tipoMiniatura, int tamanoMiniatura) =>
        new(sedeId, activoId, orden, descripcion, tipo, tamano, tipoMiniatura, tamanoMiniatura);

    public void CambiarDescripcion(string? descripcion) =>
        Descripcion = Guardas.TextoOpcional(descripcion, "La descripción de la foto", LongitudMaximaDescripcion);

    public void MarcarPrincipal(bool principal) => Principal = principal;

    /// <summary>Tipo de contenido HTTP del formato.</summary>
    public static string TipoContenido(TipoImagen tipo) => tipo switch
    {
        TipoImagen.Png => "image/png",
        TipoImagen.Webp => "image/webp",
        _ => "image/jpeg",
    };

    private static string Extension(TipoImagen tipo) => tipo switch
    {
        TipoImagen.Png => ".png",
        TipoImagen.Webp => ".webp",
        _ => ".jpg",
    };
}
