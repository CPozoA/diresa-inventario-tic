using ClosedXML.Excel;
using Inventario.Application.Auditoria;

namespace Inventario.Infrastructure.Excel;

/// <summary>Escribe el registro de auditoría en Excel (D56): hoja "Auditoría" e "Información".</summary>
internal sealed class GeneradorExcelAuditoria : IGeneradorExcelAuditoria
{
    public byte[] Generar(AuditoriaParaExcel datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        using var libro = new XLWorkbook();

        EscritorExcel.Columna<AccionParaExcel>[] red = datos.ConRed ? [new("Red", 24, r => r.Red)] : [];
        EscritorExcel.Columna<AccionParaExcel>[] columnas =
        [
            new("Fecha y hora", 17, r => r.Fecha, "dd/mm/yyyy hh:mm:ss"),
            new("Usuario", 28, r => r.Usuario),
            .. red,
            new("Módulo", 16, r => r.Modulo),
            new("Acción", 80, r => r.Accion),
            new("IP", 16, r => r.Ip, "@"),
        ];
        EscritorExcel.EscribirHoja(libro, "Auditoría", columnas, datos.Registros);

        var hoja = libro.Worksheets.Add("Información");
        hoja.Cell(1, 1).Value = "Registro de auditoría · " + datos.Ambito;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        var fila = 3;
        hoja.Cell(fila, 1).Value = "Generado el";
        hoja.Cell(fila, 2).Value = datos.GeneradoEn;
        hoja.Cell(fila, 2).Style.NumberFormat.Format = "dd/mm/yyyy hh:mm";
        fila++;
        hoja.Cell(fila, 1).Value = "Acciones";
        hoja.Cell(fila, 2).Value = (double)datos.Registros.Count;
        fila++;
        hoja.Cell(fila, 1).Value = "Filtros";
        hoja.Cell(fila, 2).Value = datos.Filtros.Count == 0 ? "Ninguno: todo el registro" : datos.Filtros[0];
        foreach (var filtro in datos.Filtros.Skip(1))
        {
            fila++;
            hoja.Cell(fila, 2).Value = filtro;
        }

        for (var f = 3; f <= fila; f++)
        {
            hoja.Cell(f, 1).Style.Font.Bold = true;
        }

        hoja.Column(1).Width = 16;
        hoja.Column(2).Width = 60;

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }
}
