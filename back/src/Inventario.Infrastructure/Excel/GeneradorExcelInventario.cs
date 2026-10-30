using ClosedXML.Excel;
using Inventario.Application.Activos.Exportar;

namespace Inventario.Infrastructure.Excel;

/// <summary>
/// Escribe el inventario en Excel con ClosedXML (D42, D55): hojas "Activos", "Discos", "Memoria RAM" e "Información".
/// Encabezados fijos y con filtro, fechas y montos con formato. Solo escribe: los datos ya vienen preparados.
/// </summary>
internal sealed class GeneradorExcelInventario : IGeneradorExcelInventario
{
    private const string FormatoFecha = "dd/mm/yyyy";
    private const string FormatoFechaHora = "dd/mm/yyyy hh:mm";
    private const string FormatoSoles = "#,##0.00";
    private const string FormatoEntero = "0";
    private const string FormatoTexto = "@";

    public byte[] Generar(InventarioParaExcel datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        using var libro = new XLWorkbook();

        EscritorExcel.Columna<ActivoParaExcel>[] red = datos.ConRed ? [new("Red", 22, a => a.Red)] : [];
        EscritorExcel.Columna<ActivoParaExcel>[] columnasActivo =
        [
            .. red,
            new("Código patrimonial", 18, a => a.CodigoPatrimonial, FormatoTexto),
            new("Tipo", 18, a => a.TipoActivo),
            new("Marca", 14, a => a.Marca),
            new("Modelo", 24, a => a.Modelo),
            new("N° de serie", 20, a => a.NumeroSerie, FormatoTexto),
            new("Establecimiento", 30, a => a.Establecimiento),
            new("Área", 22, a => a.Area),
            new("Ubicación física", 22, a => a.UbicacionFisica),
            new("Responsable", 30, a => a.Responsable),
            new("DNI del responsable", 14, a => a.DniResponsable, FormatoTexto),
            new("Responsable anterior (sin registrar)", 26, a => a.ResponsableAnterior),
            new("Estado", 16, a => a.Estado),
            new("Conservación", 13, a => a.Conservacion),
            new("Criticidad", 11, a => a.Criticidad),
            new("Descripción", 30, a => a.Descripcion),
            new("Procesador", 22, a => a.Procesador),
            new("Generación", 16, a => a.Generacion),
            new("Núcleos", 9, a => a.Nucleos, FormatoEntero),
            new("RAM (GB)", 10, a => a.RamGb, FormatoEntero),
            new("Almacenamiento (GB)", 14, a => a.AlmacenamientoGb, FormatoEntero),
            new("Tarjeta gráfica", 22, a => a.Grafica),
            new("Pantalla (pulgadas)", 12, a => a.PantallaPulgadas, "0.0"),
            new("Sistema operativo", 24, a => a.SistemaOperativo),
            new("Nombre de equipo", 18, a => a.NombreEquipo, FormatoTexto),
            new("Dominio o grupo de trabajo", 18, a => a.DominioGrupo),
            new("Dirección IP", 15, a => a.DireccionIp, FormatoTexto),
            new("Dirección MAC", 19, a => a.DireccionMac, FormatoTexto),
            new("Antivirus o EDR", 18, a => a.Antivirus),
            new("Disco cifrado", 10, a => a.DiscoCifrado),
            new("Puertos y conectividad", 30, a => a.Puertos),
            new("Accesorios", 26, a => a.Accesorios),
            new("Fecha de adquisición", 13, a => a.FechaAdquisicion, FormatoFecha),
            new("Tipo de adquisición", 16, a => a.TipoAdquisicion),
            new("Valor (S/)", 13, a => a.ValorAdquisicion, FormatoSoles),
            new("Proveedor", 26, a => a.Proveedor),
            new("N° de orden de compra", 16, a => a.NumeroOrdenCompra, FormatoTexto),
            new("Vida útil (años)", 10, a => a.VidaUtilAnios, FormatoEntero),
            new("Fin de garantía", 13, a => a.FinGarantia, FormatoFecha),
            new("Garantía", 11, a => a.Garantia),
            new("Renovación prevista", 13, a => a.FechaRenovacion, FormatoFecha),
            new("Renovación", 16, a => a.Renovacion),
            new("Próximo preventivo", 13, a => a.ProximoMantenimiento, FormatoFecha),
            new("Observaciones", 30, a => a.Observaciones),
            new("Registrado el", 16, a => a.RegistradoEn, FormatoFechaHora),
            new("Modificado el", 16, a => a.ModificadoEn, FormatoFechaHora),
        ];
        EscritorExcel.EscribirHoja(libro, "Activos", columnasActivo, datos.Activos);

        EscritorExcel.Columna<DiscoParaExcel>[] redDisco = datos.ConRed ? [new("Red", 22, d => d.Red)] : [];
        EscritorExcel.Columna<DiscoParaExcel>[] columnasDisco =
        [
            .. redDisco,
            new("Código patrimonial", 18, d => d.CodigoPatrimonial, FormatoTexto),
            new("Ranura", 12, d => d.Ranura),
            new("Tipo", 14, d => d.Tipo),
            new("Capacidad (GB)", 13, d => d.CapacidadGb, FormatoEntero),
            new("Uso", 11, d => d.Uso),
            new("Marca y modelo", 28, d => d.MarcaModelo),
            new("N° de serie", 22, d => d.NumeroSerie, FormatoTexto),
        ];
        EscritorExcel.EscribirHoja(libro, "Discos", columnasDisco, datos.Discos);

        EscritorExcel.Columna<ModuloRamParaExcel>[] redRam = datos.ConRed ? [new("Red", 22, r => r.Red)] : [];
        EscritorExcel.Columna<ModuloRamParaExcel>[] columnasRam =
        [
            .. redRam,
            new("Código patrimonial", 18, r => r.CodigoPatrimonial, FormatoTexto),
            new("Ranura", 12, r => r.Ranura),
            new("Tipo", 11, r => r.Tipo),
            new("Capacidad (GB)", 13, r => r.CapacidadGb, FormatoEntero),
            new("Velocidad (MHz)", 13, r => r.VelocidadMhz, FormatoEntero),
            new("Marca y modelo", 30, r => r.MarcaModelo),
        ];
        EscritorExcel.EscribirHoja(libro, "Memoria RAM", columnasRam, datos.ModulosRam);

        EscribirInformacion(libro, datos);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        return memoria.ToArray();
    }

    private static void EscribirInformacion(XLWorkbook libro, InventarioParaExcel datos)
    {
        var hoja = libro.Worksheets.Add("Información");
        var fila = 1;

        void Linea(string etiqueta, object valor, string? formato = null)
        {
            hoja.Cell(fila, 1).Value = etiqueta;
            hoja.Cell(fila, 1).Style.Font.Bold = true;
            EscritorExcel.Escribir(hoja.Cell(fila, 2), valor);
            if (formato is not null)
            {
                hoja.Cell(fila, 2).Style.NumberFormat.Format = formato;
            }

            fila++;
        }

        hoja.Cell(fila, 1).Value = "Inventario TIC · " + datos.Ambito;
        hoja.Cell(fila, 1).Style.Font.Bold = true;
        hoja.Cell(fila, 1).Style.Font.FontSize = 14;
        fila += 2;

        Linea("Generado el", datos.GeneradoEn, FormatoFechaHora);
        Linea("Activos", datos.Activos.Count, FormatoEntero);
        Linea("Discos", datos.Discos.Count, FormatoEntero);
        Linea("Módulos de RAM", datos.ModulosRam.Count, FormatoEntero);
        Linea("Filtros", datos.Filtros.Count == 0 ? "Ninguno: todo el inventario" : datos.Filtros[0]);
        foreach (var filtro in datos.Filtros.Skip(1))
        {
            Linea(string.Empty, filtro);
        }

        fila++;
        hoja.Cell(fila, 1).Value =
            "Las hojas «Discos» y «Memoria RAM» se cruzan con «Activos» por el código patrimonial. Los activos de baja se incluyen.";

        hoja.Column(1).Width = 18;
        hoja.Column(2).Width = 60;
    }

}
