using Inventario.Domain.Activos;
using Inventario.Domain.Common;

namespace Inventario.Domain.Tests;

public sealed class FotoActivoTests
{
    private static readonly Guid Sede = Guid.NewGuid();
    private static readonly Guid Activo = Guid.NewGuid();

    /// <summary>JPEG mínima: JFIF, EXIF con GPS, perfil ICC, comentario, tabla de cuantización y datos comprimidos.</summary>
    private static byte[] JpegConMetadatos() =>
    [
        0xFF, 0xD8,
        .. Segmento(0xE0, "JFIF\0\u0001\u0001\0\0\u0001\0\u0001\0\0"u8),
        .. Segmento(0xE1, "Exif\0\0GPS -13.16,-74.22 Samsung"u8),
        .. Segmento(0xE2, "ICC_PROFILE\0\u0001\u0001perfil"u8),
        .. Segmento(0xFE, "Tomada en la oficina"u8),
        .. Segmento(0xDB, [0x00, 1, 2, 3, 4]),
        .. Segmento(0xDA, [0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]),
        0x12, 0x34, 0xFF, 0x00, 0x56,
        0xFF, 0xD9,
    ];

    private static byte[] Segmento(byte marcador, ReadOnlySpan<byte> datos)
    {
        var longitud = datos.Length + 2;
        return [0xFF, marcador, (byte)(longitud >> 8), (byte)longitud, .. datos];
    }

    private static bool Contiene(byte[] contenido, string texto) =>
        contenido.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(texto)) >= 0;

    [Fact]
    public void ElFormatoSeReconocePorElContenido_NoPorElNombre()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
        byte[] webp = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBPVP8 "u8];
        byte[] pdf = [.. "%PDF-1.7"u8];

        Assert.Equal(TipoImagen.Jpeg, ImagenFoto.Detectar(JpegConMetadatos()));
        Assert.Equal(TipoImagen.Png, ImagenFoto.Detectar(png));
        Assert.Equal(TipoImagen.Webp, ImagenFoto.Detectar(webp));
        Assert.Null(ImagenFoto.Detectar(pdf));
        Assert.Null(ImagenFoto.Detectar([]));
    }

    [Fact]
    public void AUnaJpegSeLeQuitanElExifYLosComentarios_PeroNoLaImagenNiElPerfilDeColor()
    {
        var original = JpegConMetadatos();

        var limpia = ImagenFoto.QuitarMetadatosJpeg(original)!;

        Assert.False(Contiene(limpia, "GPS"));
        Assert.False(Contiene(limpia, "Samsung"));
        Assert.False(Contiene(limpia, "oficina"));
        Assert.True(Contiene(limpia, "JFIF"));
        Assert.True(Contiene(limpia, "ICC_PROFILE"));
        Assert.True(limpia.AsSpan()[^9..].SequenceEqual(original.AsSpan()[^9..]));
        Assert.Equal(TipoImagen.Jpeg, ImagenFoto.Detectar(limpia));
    }

    [Fact]
    public void UnaJpegCortadaSeConsideraDanada()
    {
        var original = JpegConMetadatos();

        Assert.Null(ImagenFoto.QuitarMetadatosJpeg(original.AsSpan(0, 20)));
        Assert.Null(ImagenFoto.QuitarMetadatosJpeg([0xFF, 0xD8, 0x00, 0x00]));
    }

    [Fact]
    public void LasClavesSeparanLasFotosPorSedeYActivo()
    {
        var foto = FotoActivo.Crear(Sede, Activo, 0, "  Placa patrimonial ", TipoImagen.Jpeg, 300_000, TipoImagen.Webp, 20_000);

        Assert.Equal("Placa patrimonial", foto.Descripcion);
        Assert.Equal($"{Sede:N}/{Activo:N}/{foto.Id:N}.jpg", foto.Clave);
        Assert.Equal($"{Sede:N}/{Activo:N}/{foto.Id:N}-mini.webp", foto.ClaveMiniatura);
    }

    [Fact]
    public void LaDescripcionEsOpcional_YNoPasaDe60Caracteres()
    {
        var foto = FotoActivo.Crear(Sede, Activo, 0, null, TipoImagen.Png, 1_000, TipoImagen.Png, 1_000);

        foto.CambiarDescripcion("   ");
        Assert.Null(foto.Descripcion);
        Assert.Throws<DominioException>(() => foto.CambiarDescripcion(new string('x', 61)));
    }

    [Theory]
    [InlineData(0, 1_000)]
    [InlineData(FotoActivo.TamanoMaximoBytes + 1, 1_000)]
    [InlineData(1_000, FotoActivo.TamanoMaximoMiniaturaBytes + 1)]
    public void UnTamanoFueraDeLimitesSeRechaza(int tamano, int tamanoMiniatura) =>
        Assert.Throws<DominioException>(() =>
            FotoActivo.Crear(Sede, Activo, 0, null, TipoImagen.Jpeg, tamano, TipoImagen.Jpeg, tamanoMiniatura));
}
