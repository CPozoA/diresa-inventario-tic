using System.Globalization;
using System.Text;
using Inventario.Application.Activos.Consultar;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos.Exportar;

/// <summary>
/// El inventario en Excel (D55), con los mismos filtros de la lista: hoja "Activos" (una fila por activo con todos sus
/// datos), "Discos", "Memoria RAM" e "Información" (cuándo, de qué red y con qué filtros).
/// </summary>
/// <param name="Filtros">Los de la lista del inventario.</param>
/// <remarks>Requiere Activos.Ver.</remarks>
public sealed record ExportarInventarioQuery(FiltrosInventario Filtros) : IQuery<ArchivoDescargable>;

/// <summary>Errores de la exportación.</summary>
public static class ErroresExportacion
{
    public static ErrorAplicacion DemasiadosActivos(int total) =>
        ErrorAplicacion.ReglaDeNegocio(
            "Exportacion.DemasiadosActivos",
            string.Create(
                CultureInfo.GetCultureInfo("es-PE"),
                $"Son {total:N0} activos; se exportan hasta {ExportarInventarioHandler.MaximoActivos:N0} a la vez. Filtre por red o establecimiento."));
}

internal sealed class ExportarInventarioHandler(IAppDbContext db, IUsuarioActual usuario, TimeProvider reloj, IGeneradorExcelInventario generador)
    : IQueryHandler<ExportarInventarioQuery, ArchivoDescargable>
{
    public const int MaximoActivos = 20_000;

    public async Task<Result<ArchivoDescargable>> Handle(ExportarInventarioQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query.Filtros);

        var ahora = reloj.GetLocalNow().DateTime;
        var hoy = DateOnly.FromDateTime(ahora);
        var filtros = query.Filtros;

        var ids = ConsultaInventario.Filas(db, filtros, hoy).Select(f => f.Id);
        var total = await ids.CountAsync(cancellationToken);
        if (total > MaximoActivos)
        {
            return ErroresExportacion.DemasiadosActivos(total);
        }

        var filas = await (
            from a in db.Activos.AsNoTracking()
            where ids.Contains(a.Id)
            join t in db.TiposActivo on a.TipoActivoId equals t.Id
            join m in db.Marcas on a.MarcaId equals m.Id
            join ar in db.Areas on a.AreaId equals ar.Id
            join es in db.EstadosActivo on a.EstadoActivoId equals es.Id
            join e in db.Establecimientos on a.EstablecimientoId equals e.Id
            join s in db.Sedes on a.SedeId equals s.Id
            orderby s.Nombre, a.CodigoPatrimonial
            select new
            {
                Activo = a,
                Red = s.Nombre,
                TipoActivo = t.Nombre,
                Marca = m.Nombre,
                Area = ar.Nombre,
                Estado = es.Nombre,
                Establecimiento = e.Nombre,
                Procesador = db.Procesadores.Where(p => p.Id == a.ProcesadorId).Select(p => new { p.Nombre, p.Generaciones }).FirstOrDefault(),
                SistemaOperativo = db.SistemasOperativos.Where(x => x.Id == a.SistemaOperativoId).Select(x => x.Nombre).FirstOrDefault(),
                Antivirus = db.Antivirus.Where(x => x.Id == a.AntivirusId).Select(x => x.Nombre).FirstOrDefault(),
                Persona = db.Asignaciones
                    .Where(x => x.ActivoId == a.Id && x.FechaDevolucion == null)
                    .Join(db.Personal, x => x.PersonaId, p => p.Id, (x, p) => new { p.NombreCompleto, p.Dni })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var discos = await (
            from a in db.Activos.AsNoTracking()
            where ids.Contains(a.Id)
            join s in db.Sedes on a.SedeId equals s.Id
            from d in a.Discos
            orderby s.Nombre, a.CodigoPatrimonial, d.Orden
            select new { Red = s.Nombre, a.Id, a.CodigoPatrimonial, d.Ranura, d.Tipo, d.CapacidadGb, d.Uso, d.MarcaModelo, d.NumeroSerie })
            .ToListAsync(cancellationToken);

        var modulos = await (
            from a in db.Activos.AsNoTracking()
            where ids.Contains(a.Id)
            join s in db.Sedes on a.SedeId equals s.Id
            from r in a.ModulosRam
            orderby s.Nombre, a.CodigoPatrimonial, r.Orden
            select new { Red = s.Nombre, a.Id, a.CodigoPatrimonial, r.Ranura, r.Tipo, r.CapacidadGb, r.VelocidadMhz, r.MarcaModelo })
            .ToListAsync(cancellationToken);

        var ramPorActivo = modulos.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.Sum(r => r.CapacidadGb));
        var discoPorActivo = discos.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.Sum(d => d.CapacidadGb));

        var activos = filas.Select(f =>
        {
            var a = f.Activo;
            return new ActivoParaExcel
            {
                Red = f.Red,
                CodigoPatrimonial = a.CodigoPatrimonial,
                TipoActivo = f.TipoActivo,
                Marca = f.Marca,
                Modelo = a.Modelo,
                NumeroSerie = a.NumeroSerie,
                Establecimiento = f.Establecimiento,
                Area = f.Area,
                UbicacionFisica = a.UbicacionFisica,
                Responsable = f.Persona?.NombreCompleto,
                DniResponsable = f.Persona?.Dni,
                ResponsableAnterior = f.Persona is null ? a.Responsable : null,
                Estado = f.Estado,
                Conservacion = ListasActivo.Texto(a.Conservacion),
                Criticidad = a.Criticidad is Criticidad c ? ListasActivo.Texto(c) : null,
                Descripcion = a.Descripcion,
                Procesador = f.Procesador?.Nombre,
                Generacion = a.GeneracionProcesador is string g && f.Procesador is not null
                    ? GeneracionesDeProcesador.Buscar(f.Procesador.Generaciones, g)?.Texto ?? g
                    : null,
                Nucleos = a.Nucleos,
                RamGb = ramPorActivo.TryGetValue(a.Id, out var ram) ? ram : null,
                AlmacenamientoGb = discoPorActivo.TryGetValue(a.Id, out var disco) ? disco : null,
                Grafica = Grafica(a.TipoGrafica, a.ModeloGrafica),
                PantallaPulgadas = a.PantallaPulgadas,
                SistemaOperativo = Unir(f.SistemaOperativo, a.VersionSistemaOperativo),
                NombreEquipo = a.NombreEquipo,
                DominioGrupo = a.DominioGrupo,
                DireccionIp = a.DireccionIp,
                DireccionMac = a.DireccionMac,
                Antivirus = f.Antivirus,
                DiscoCifrado = a.DiscoCifrado switch { true => "Sí", false => "No", null => null },
                Puertos = Lista(a.Puertos),
                Accesorios = Lista(a.Accesorios),
                FechaAdquisicion = a.FechaAdquisicion,
                TipoAdquisicion = a.TipoAdquisicion is TipoAdquisicion ta ? ListasActivo.Texto(ta) : null,
                ValorAdquisicion = a.ValorAdquisicion,
                Proveedor = a.Proveedor,
                NumeroOrdenCompra = a.NumeroOrdenCompra,
                VidaUtilAnios = a.VidaUtilAnios,
                FinGarantia = a.FinGarantia,
                Garantia = ListasActivo.Texto(Activo.GarantiaAl(a.FinGarantia, hoy)),
                FechaRenovacion = a.FechaRenovacion,
                Renovacion = ListasActivo.Texto(Activo.RenovacionAl(a.FechaRenovacion, hoy)),
                ProximoMantenimiento = a.ProximoMantenimiento,
                Observaciones = a.Observaciones,
                RegistradoEn = a.CreadoEn.ToLocalTime().DateTime,
                ModificadoEn = a.ModificadoEn?.ToLocalTime().DateTime,
            };
        }).ToList();

        var conRed = usuario.EsSuperAdmin && filtros.SedeId is null;
        var ambito = conRed ? "Todas las redes" : await NombreRedAsync(filtros.SedeId ?? usuario.SedeId, cancellationToken);

        var datos = new InventarioParaExcel(
            ambito,
            ahora,
            await DescribirFiltrosAsync(filtros, cancellationToken),
            conRed,
            activos,
            discos.Select(d => new DiscoParaExcel(
                d.Red, d.CodigoPatrimonial, d.Ranura, ListasActivo.Texto(d.Tipo), d.CapacidadGb, ListasActivo.Texto(d.Uso), d.MarcaModelo, d.NumeroSerie)).ToList(),
            modulos.Select(r => new ModuloRamParaExcel(
                r.Red, r.CodigoPatrimonial, r.Ranura, ListasActivo.Texto(r.Tipo), r.CapacidadGb, r.VelocidadMhz, r.MarcaModelo)).ToList());

        var nombre = $"Inventario-{Slug(ambito)}-{hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.xlsx";
        return new ArchivoDescargable(generador.Generar(datos), nombre);
    }

    private async Task<string> NombreRedAsync(Guid? sedeId, CancellationToken cancellationToken) =>
        sedeId is Guid id
            ? await db.Sedes.AsNoTracking().Where(s => s.Id == id).Select(s => s.Nombre).FirstOrDefaultAsync(cancellationToken) ?? "Red"
            : "Red";

    /// <summary>Los filtros en palabras, para la hoja "Información".</summary>
    private async Task<IReadOnlyList<string>> DescribirFiltrosAsync(FiltrosInventario f, CancellationToken cancellationToken)
    {
        var lista = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            lista.Add($"Búsqueda: «{f.Buscar.Trim()}»");
        }

        if (f.EstablecimientoId is Guid establecimiento)
        {
            lista.Add("Establecimiento: " + await db.Establecimientos.AsNoTracking().Where(e => e.Id == establecimiento).Select(e => e.Nombre).FirstOrDefaultAsync(cancellationToken));
        }

        if (f.TipoActivoId is Guid tipo)
        {
            lista.Add("Tipo: " + await db.TiposActivo.AsNoTracking().Where(t => t.Id == tipo).Select(t => t.Nombre).FirstOrDefaultAsync(cancellationToken));
        }

        if (f.AreaId is Guid area)
        {
            lista.Add("Área: " + await db.Areas.AsNoTracking().Where(a => a.Id == area).Select(a => a.Nombre).FirstOrDefaultAsync(cancellationToken));
        }

        if (f.EstadoActivoId is Guid estado)
        {
            lista.Add("Estado: " + await db.EstadosActivo.AsNoTracking().Where(e => e.Id == estado).Select(e => e.Nombre).FirstOrDefaultAsync(cancellationToken));
        }

        if (f.Garantia is EstadoGarantia garantia)
        {
            lista.Add("Garantía: " + ListasActivo.Texto(garantia));
        }

        if (f.Renovacion is SituacionRenovacion renovacion)
        {
            lista.Add("Renovación: " + ListasActivo.Texto(renovacion));
        }

        if (f.Atencion is MotivoAtencion motivo)
        {
            lista.Add("Requieren atención: " + motivo switch
            {
                MotivoAtencion.FueraDeServicio => "fuera de servicio",
                MotivoAtencion.Renovar => "renovar",
                MotivoAtencion.ProximoARenovar => "próximos a renovar",
                MotivoAtencion.EnMantenimiento => "en mantenimiento",
                MotivoAtencion.GarantiaVencida => "garantía vencida",
                MotivoAtencion.MantenimientoVencido => "preventivo vencido",
                _ => "cualquier motivo",
            });
        }

        if (f.Falta is DatoFaltante falta)
        {
            lista.Add("Fichas por completar: " + falta switch
            {
                DatoFaltante.Fotos => "sin fotos",
                DatoFaltante.FechaAdquisicion => "sin fecha de adquisición",
                DatoFaltante.Responsable => "sin responsable",
                _ => "sin número de serie",
            });
        }

        return lista;
    }

    private static string? Grafica(TipoGrafica? tipo, string? modelo) =>
        tipo is TipoGrafica t ? Unir(ListasActivo.Texto(t), modelo) : modelo;

    private static string? Unir(string? primero, string? segundo) =>
        (primero, segundo) switch
        {
            (null, null) => null,
            (string p, null) => p,
            (null, string s) => s,
            (string p, string s) => $"{p} · {s}",
        };

    private static string? Lista<T>(IReadOnlyList<T> valores)
        where T : struct, Enum =>
        valores.Count == 0 ? null : string.Join(", ", valores.Select(v => ListasActivo.Texto(v)));

    /// <summary>"Red de Salud Huanta" → "Red-de-Salud-Huanta" (sin tildes ni signos, para el nombre del archivo).</summary>
    internal static string Slug(string texto)
    {
        var resultado = new StringBuilder();
        foreach (var caracter in texto.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(caracter))
            {
                resultado.Append(caracter);
            }
            else if ((char.IsWhiteSpace(caracter) || caracter is '-' or '_' or '.') && resultado.Length > 0 && resultado[^1] != '-')
            {
                resultado.Append('-');
            }
        }

        return resultado.ToString().Trim('-');
    }
}
