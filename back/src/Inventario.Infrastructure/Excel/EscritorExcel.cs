using ClosedXML.Excel;

namespace Inventario.Infrastructure.Excel;

/// <summary>Escritura común de las hojas de datos (inventario, auditoría): encabezado fijo con filtro y valores con su tipo.</summary>
internal static class EscritorExcel
{
    private static readonly XLColor FondoEncabezado = XLColor.FromHtml("#E3E9FA");

    public static void EscribirHoja<T>(XLWorkbook libro, string nombre, IReadOnlyList<Columna<T>> columnas, IReadOnlyList<T> filas)
    {
        var hoja = libro.Worksheets.Add(nombre);

        for (var c = 0; c < columnas.Count; c++)
        {
            var columna = columnas[c];
            hoja.Cell(1, c + 1).Value = columna.Titulo;
            hoja.Column(c + 1).Width = columna.Ancho;
            if (columna.Formato is string formato)
            {
                hoja.Column(c + 1).Style.NumberFormat.Format = formato;
            }
        }

        for (var f = 0; f < filas.Count; f++)
        {
            for (var c = 0; c < columnas.Count; c++)
            {
                Escribir(hoja.Cell(f + 2, c + 1), columnas[c].Valor(filas[f]));
            }
        }

        var encabezado = hoja.Row(1);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Fill.BackgroundColor = FondoEncabezado;
        encabezado.Style.Alignment.WrapText = true;
        encabezado.Style.NumberFormat.Format = "General";
        hoja.SheetView.FreezeRows(1);

        // Filtro de Excel en los encabezados (también con la hoja vacía, para que se vean los nombres).
        hoja.Range(1, 1, Math.Max(filas.Count, 1) + 1, columnas.Count).SetAutoFilter();
    }

    /// <summary>Escribe el valor con su tipo (texto, número o fecha). Nulo: la celda queda vacía.</summary>
    public static void Escribir(IXLCell celda, object? valor)
    {
        switch (valor)
        {
            case null:
                break;
            case string texto:
                celda.Value = texto;
                break;
            case DateTime fecha:
                celda.Value = fecha;
                break;
            case DateOnly dia:
                celda.Value = dia.ToDateTime(TimeOnly.MinValue);
                break;
            case int entero:
                celda.Value = (double)entero;
                break;
            case decimal monto:
                celda.Value = (double)monto;
                break;
            case double numero:
                celda.Value = numero;
                break;
            default:
                celda.Value = Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                break;
        }
    }

    /// <summary>Una columna: encabezado, ancho (en caracteres; fijo, para que no dependa de las fuentes del servidor), valor y formato.</summary>
    public sealed record Columna<T>(string Titulo, double Ancho, Func<T, object?> Valor, string? Formato = null);
}
