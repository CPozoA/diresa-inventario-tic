using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Inventario.Application.Establecimientos.Importar;

namespace Inventario.Infrastructure.Excel;

/// <summary>
/// Lee y genera la hoja de establecimientos con ClosedXML (docs/ARQUITECTURA.md D42).
/// Solo traduce entre Excel y texto: las reglas (números, red, duplicados) las aplica Application.
/// </summary>
internal sealed class HojaEstablecimientosExcel : IHojaEstablecimientos
{
    private const string NombreHoja = "Establecimientos";

    public LecturaEstablecimientos Leer(Stream archivo)
    {
        ArgumentNullException.ThrowIfNull(archivo);

        XLWorkbook libro;
        try
        {
            libro = new XLWorkbook(archivo);
        }
#pragma warning disable CA1031 // Cualquier falla al abrir significa lo mismo para el usuario: no es un Excel válido.
        catch (Exception)
#pragma warning restore CA1031
        {
            return LecturaEstablecimientos.ConError("El archivo no es un Excel (.xlsx) válido. Guárdelo como «Libro de Excel» e inténtelo de nuevo.");
        }

        using (libro)
        {
            var hoja = libro.Worksheets.FirstOrDefault();
            var encabezado = hoja?.FirstRowUsed();
            if (hoja is null || encabezado is null)
            {
                return LecturaEstablecimientos.ConError("La primera hoja del archivo está vacía.");
            }

            var columnas = ReconocerColumnas(encabezado);
            var faltantes = ColumnasEstablecimientos.Obligatorias.Where(c => !columnas.ContainsKey(Clave(c))).ToList();
            if (faltantes.Count > 0)
            {
                return LecturaEstablecimientos.ConError(
                    $"Faltan columnas en la primera fila: {string.Join(", ", faltantes)}. Use la plantilla para ver los encabezados esperados.");
            }

            var filas = new List<FilaEstablecimiento>();
            foreach (var fila in hoja.RowsUsed().Where(f => f.RowNumber() > encabezado.RowNumber()))
            {
                string? Valor(string columna) =>
                    columnas.TryGetValue(Clave(columna), out var numero) ? Texto(fila.Cell(numero)) : null;

                var leida = new FilaEstablecimiento(
                    fila.RowNumber(),
                    Valor("Id_Establecimiento"),
                    Valor("Codigo_Unico"),
                    Valor("Nombre"),
                    Valor("Microred"),
                    Valor("Red"),
                    Valor("Provincia"),
                    Valor("Distrito"),
                    Valor("Ubigeo"),
                    Valor("Categoria"));

                // Una fila con formato pero sin datos en las columnas conocidas no cuenta.
                if (leida is { IdEstablecimiento: null, CodigoUnico: null, Nombre: null, Red: null })
                {
                    continue;
                }

                filas.Add(leida);
            }

            return new LecturaEstablecimientos(filas, null);
        }
    }

    public byte[] CrearPlantilla()
    {
        using var libro = new XLWorkbook();

        var hoja = libro.Worksheets.Add(NombreHoja);
        var columnas = ColumnasEstablecimientos.Todas;
        for (var i = 0; i < columnas.Count; i++)
        {
            hoja.Cell(1, i + 1).Value = columnas[i];
        }

        hoja.Row(1).Style.Font.Bold = true;
        hoja.SheetView.FreezeRows(1);

        // Ubigeo como texto, para que Excel no quite el 0 inicial (050601).
        var columnaUbigeo = columnas.ToList().IndexOf("Ubigeo") + 1;
        hoja.Column(columnaUbigeo).Style.NumberFormat.Format = "@";
        // Anchos fijos (no se calculan con fuentes: así funciona igual en cualquier servidor).
        hoja.Columns(1, columnas.Count).Width = 18;
        hoja.Column(columnas.ToList().IndexOf("Nombre") + 1).Width = 45;
        hoja.Column(columnas.ToList().IndexOf("Microred") + 1).Width = 30;

        var instrucciones = libro.Worksheets.Add("Instrucciones");
        string[] lineas =
        [
            "Cómo llenar la hoja «Establecimientos»",
            "Una fila por establecimiento, debajo de los encabezados. No cambie el nombre de las columnas.",
            "Obligatorias: Id_Establecimiento, Codigo_Unico, Nombre y Red. Id_Establecimiento y Codigo_Unico son números enteros.",
            "Red: el nombre de la red de salud (por ejemplo «Huanta» o «Red de Salud Huanta»). Se convierte en la sede del sistema.",
            "Ubigeo: 6 dígitos, por ejemplo 050601. Si Excel quita el 0 inicial, el sistema lo completa.",
            "Si un Id_Establecimiento ya existe en el sistema, la fila actualiza sus datos. Un establecimiento no puede cambiar de red.",
            "Si alguna fila tiene errores, no se guarda nada: el sistema indica qué filas corregir.",
        ];
        for (var i = 0; i < lineas.Length; i++)
        {
            instrucciones.Cell(i + 1, 1).Value = lineas[i];
        }

        instrucciones.Row(1).Style.Font.Bold = true;
        instrucciones.Column(1).Width = 110;

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    /// <summary>Columnas reconocidas: clave normalizada del encabezado → número de columna.</summary>
    private static Dictionary<string, int> ReconocerColumnas(IXLRow encabezado)
    {
        var columnas = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var celda in encabezado.CellsUsed())
        {
            var clave = Clave(Texto(celda));
            if (clave.Length > 0)
            {
                columnas.TryAdd(clave, celda.Address.ColumnNumber);
            }
        }

        return columnas;
    }

    /// <summary>"Código_Único", "codigo unico" y "CODIGO UNICO" dan la misma clave: CODIGOUNICO.</summary>
    private static string Clave(string? encabezado)
    {
        if (string.IsNullOrWhiteSpace(encabezado))
        {
            return string.Empty;
        }

        var resultado = new StringBuilder();
        foreach (var caracter in encabezado.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(caracter))
            {
                resultado.Append(char.ToUpperInvariant(caracter));
            }
        }

        return resultado.ToString();
    }

    /// <summary>Valor de la celda como texto. Los números enteros salen sin decimales ni separadores (2345, no 2345.0).</summary>
    private static string? Texto(IXLCell celda)
    {
        var valor = celda.Value;

        if (valor.IsBlank)
        {
            return null;
        }

        if (valor.IsNumber)
        {
            var numero = valor.GetNumber();
            return numero == Math.Floor(numero) && Math.Abs(numero) < 1e15
                ? ((long)numero).ToString(CultureInfo.InvariantCulture)
                : numero.ToString(CultureInfo.InvariantCulture);
        }

        var texto = valor.IsText ? valor.GetText() : celda.GetString();
        var limpio = texto.Trim();
        return limpio.Length == 0 ? null : limpio;
    }
}
