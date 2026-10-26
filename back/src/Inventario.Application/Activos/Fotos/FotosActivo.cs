using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;

namespace Inventario.Application.Activos.Fotos;

/// <summary>Una foto de un activo, sin el archivo (se pide aparte).</summary>
/// <param name="Id">Identificador; con él se arma la dirección del archivo.</param>
/// <param name="Descripcion">"Placa patrimonial"… Opcional.</param>
/// <param name="Principal">La que se muestra en la lista y primero en la ficha.</param>
/// <param name="Orden">Orden de subida.</param>
/// <param name="TamanoBytes">Tamaño de la foto guardada.</param>
/// <param name="CreadoEn">Cuándo se subió.</param>
public sealed record FotoActivoDto(Guid Id, string? Descripcion, bool Principal, int Orden, int TamanoBytes, DateTimeOffset CreadoEn);

/// <summary>Archivo de una foto, listo para enviar.</summary>
public sealed record ArchivoFotoDto(Stream Contenido, string TipoContenido);

/// <summary>Errores esperados de las fotos.</summary>
public static class ErroresFoto
{
    public static readonly ErrorAplicacion NoEncontrada =
        ErrorAplicacion.NoEncontrado("Fotos.NoEncontrada", "La foto no existe.");

    public static readonly ErrorAplicacion Maximo =
        ErrorAplicacion.ReglaDeNegocio(
            "Fotos.Maximo",
            $"El activo ya tiene {FotoActivo.MaximoPorActivo} fotos, el máximo. Quite una para agregar otra.");

    public static readonly ErrorAplicacion SinArchivo =
        ErrorAplicacion.Validacion("Fotos.SinArchivo", "Elija la foto que desea subir.");

    public static readonly ErrorAplicacion TipoNoValido =
        ErrorAplicacion.Validacion("Fotos.TipoNoValido", "El archivo no es una imagen JPG, PNG o WebP.");

    public static readonly ErrorAplicacion MuyGrande =
        ErrorAplicacion.Validacion(
            "Fotos.MuyGrande",
            $"La foto pesa más de {FotoActivo.TamanoMaximoBytes / (1024 * 1024)} MB. Vuelva a subirla desde la aplicación, que la reduce.");

    public static readonly ErrorAplicacion SinMiniatura =
        ErrorAplicacion.Validacion("Fotos.SinMiniatura", "Falta la miniatura de la foto. Vuelva a subirla desde la aplicación.");

    public static readonly ErrorAplicacion Danada =
        ErrorAplicacion.Validacion("Fotos.Danada", "No se pudo leer la imagen; puede estar dañada.");
}

internal static class ProyeccionFoto
{
    public static FotoActivoDto ADto(FotoActivo f) =>
        new(f.Id, f.Descripcion, f.Principal, f.Orden, f.TamanoBytes, f.CreadoEn);
}
