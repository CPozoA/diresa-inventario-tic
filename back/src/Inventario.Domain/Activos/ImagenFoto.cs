namespace Inventario.Domain.Activos;

/// <summary>
/// Revisa las imágenes que se suben: el formato se reconoce por los primeros bytes (no por el nombre), y a las JPEG
/// se les quitan los metadatos (EXIF: ubicación GPS, modelo del celular, fecha) antes de guardarlas.
/// </summary>
public static class ImagenFoto
{
    /// <summary>Formato de la imagen, o nulo si no es JPEG, PNG ni WebP.</summary>
    public static TipoImagen? Detectar(ReadOnlySpan<byte> contenido)
    {
        if (contenido.Length >= 3 && contenido[0] == 0xFF && contenido[1] == 0xD8 && contenido[2] == 0xFF)
        {
            return TipoImagen.Jpeg;
        }

        if (contenido.Length >= 8 && contenido[..8].SequenceEqual(FirmaPng))
        {
            return TipoImagen.Png;
        }

        if (contenido.Length >= 12 && contenido[..4].SequenceEqual("RIFF"u8) && contenido[8..12].SequenceEqual("WEBP"u8))
        {
            return TipoImagen.Webp;
        }

        return null;
    }

    /// <summary>
    /// Copia de una JPEG sin los bloques de metadatos (APP1 a APP15 salvo el perfil de color ICC, y los comentarios).
    /// La imagen en sí no cambia. Devuelve nulo si la estructura no es válida.
    /// </summary>
    public static byte[]? QuitarMetadatosJpeg(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            return null;
        }

        var salida = new List<byte>(jpeg.Length) { 0xFF, 0xD8 };
        var posicion = 2;

        while (posicion < jpeg.Length)
        {
            if (jpeg[posicion] != 0xFF)
            {
                return null;
            }

            // Bytes de relleno (0xFF repetidos) antes del marcador.
            while (posicion < jpeg.Length && jpeg[posicion] == 0xFF)
            {
                posicion++;
            }

            if (posicion >= jpeg.Length)
            {
                return null;
            }

            var marcador = jpeg[posicion];
            posicion++;

            // Fin de imagen o marcadores sin longitud.
            if (marcador == 0xD9)
            {
                salida.Add(0xFF);
                salida.Add(0xD9);
                return [.. salida];
            }

            if (marcador is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                salida.Add(0xFF);
                salida.Add(marcador);
                continue;
            }

            if (posicion + 2 > jpeg.Length)
            {
                return null;
            }

            var longitud = (jpeg[posicion] << 8) | jpeg[posicion + 1];
            if (longitud < 2 || posicion + longitud > jpeg.Length)
            {
                return null;
            }

            var segmento = jpeg.Slice(posicion, longitud);

            // Inicio de los datos comprimidos: desde aquí se copia todo tal cual.
            if (marcador == 0xDA)
            {
                salida.Add(0xFF);
                salida.Add(marcador);
                salida.AddRange(jpeg[posicion..]);
                return [.. salida];
            }

            if (!EsMetadato(marcador, segmento))
            {
                salida.Add(0xFF);
                salida.Add(marcador);
                salida.AddRange(segmento);
            }

            posicion += longitud;
        }

        return null;
    }

    private static ReadOnlySpan<byte> FirmaPng => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>APP1–APP15 (EXIF, XMP, IPTC…) y comentarios; se conservan APP0 (JFIF) y el perfil ICC (APP2).</summary>
    private static bool EsMetadato(byte marcador, ReadOnlySpan<byte> segmento)
    {
        if (marcador == 0xFE)
        {
            return true;
        }

        if (marcador is < 0xE1 or > 0xEF)
        {
            return false;
        }

        var esIcc = marcador == 0xE2 && segmento.Length >= 14 && segmento[2..14].SequenceEqual("ICC_PROFILE\0"u8);
        return !esIcc;
    }
}
