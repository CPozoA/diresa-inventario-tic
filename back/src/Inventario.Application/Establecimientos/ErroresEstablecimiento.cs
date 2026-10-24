using Inventario.Application.Common.Resultados;
using Inventario.Application.Establecimientos.Importar;

namespace Inventario.Application.Establecimientos;

/// <summary>Errores esperados de la gestión de establecimientos.</summary>
public static class ErroresEstablecimiento
{
    public static readonly ErrorAplicacion NoEncontrado =
        ErrorAplicacion.NoEncontrado("Establecimientos.NoEncontrado", "El establecimiento no existe.");

    public static readonly ErrorAplicacion SedeNoEncontrada =
        ErrorAplicacion.NoEncontrado("Establecimientos.SedeNoEncontrada", "La red (sede) indicada no existe.");

    public static readonly ErrorAplicacion IdDuplicado =
        ErrorAplicacion.Conflicto("Establecimientos.IdDuplicado", "Ya existe un establecimiento con ese Id_Establecimiento.");

    public static readonly ErrorAplicacion CodigoUnicoDuplicado =
        ErrorAplicacion.Conflicto("Establecimientos.CodigoUnicoDuplicado", "Ya existe un establecimiento con ese código único.");

    public static readonly ErrorAplicacion ArchivoVacio =
        ErrorAplicacion.Validacion("Establecimientos.ArchivoVacio", "Elija un archivo de Excel (.xlsx).");

    public static readonly ErrorAplicacion ArchivoMuyGrande =
        ErrorAplicacion.Validacion(
            "Establecimientos.ArchivoMuyGrande",
            $"El archivo supera el tamaño máximo de {ImportarEstablecimientosHandler.TamanoMaximoMb} MB.");

    public static ErrorAplicacion ArchivoInvalido(string detalle) =>
        ErrorAplicacion.Validacion("Establecimientos.ArchivoInvalido", detalle);
}
