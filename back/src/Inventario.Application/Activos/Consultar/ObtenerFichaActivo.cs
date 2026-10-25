using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos.Consultar;

/// <summary>Elemento de catálogo (o área) referido por la ficha: su identificador y su nombre.</summary>
public sealed record ReferenciaDto(Guid Id, string Nombre);

/// <summary>Establecimiento del activo, con los datos que se muestran en la ficha.</summary>
public sealed record EstablecimientoActivoDto(
    Guid Id,
    int CodigoUnico,
    string Nombre,
    string? Categoria,
    string? Microred,
    string? Provincia,
    string? Distrito);

/// <summary>Identificación, ubicación y estado, con los nombres de los catálogos.</summary>
public sealed record GeneralesActivoDto(
    string CodigoPatrimonial,
    ReferenciaDto TipoActivo,
    ReferenciaDto Marca,
    string Modelo,
    string? NumeroSerie,
    string? Descripcion,
    ReferenciaDto Area,
    string? UbicacionFisica,
    string? Responsable,
    ReferenciaDto Estado,
    Conservacion Conservacion,
    Criticidad? Criticidad,
    string? Observaciones);

/// <summary>Características técnicas, con el nombre del sistema operativo.</summary>
public sealed record TecnicosActivoDto(
    ReferenciaDto? Procesador,
    OpcionDto? Generacion,
    int? Nucleos,
    TipoGrafica? TipoGrafica,
    string? ModeloGrafica,
    decimal? PantallaPulgadas,
    ReferenciaDto? SistemaOperativo,
    string? VersionSistemaOperativo,
    string? NombreEquipo,
    string? DominioGrupo,
    string? DireccionIp,
    string? DireccionMac,
    ReferenciaDto? Antivirus,
    bool? DiscoCifrado,
    IReadOnlyList<OpcionDto> Puertos,
    IReadOnlyList<OpcionDto> Accesorios);

/// <summary>Ficha técnica completa de un activo, con los datos calculados a hoy.</summary>
/// <remarks><c>Ficha</c> es la del tipo de activo (D43): indica qué secciones técnicas se muestran.</remarks>
public sealed record FichaActivoDto(
    Guid Id,
    FichaTecnica Ficha,
    Guid SedeId,
    string SedeNombre,
    EstablecimientoActivoDto Establecimiento,
    GeneralesActivoDto Generales,
    DatosAdquisicionActivo Adquisicion,
    TecnicosActivoDto Tecnicos,
    IReadOnlyList<DatosDisco> Discos,
    IReadOnlyList<DatosModuloRam> ModulosRam,
    int RamTotalGb,
    int AlmacenamientoTotalGb,
    SituacionActivo Situacion,
    DateTimeOffset CreadoEn,
    DateTimeOffset? ModificadoEn);

/// <summary>Ficha técnica de un activo. Un activo de otra sede "no existe".</summary>
/// <remarks>Requiere Activos.Ver.</remarks>
public sealed record ObtenerFichaActivoQuery(Guid Id) : IQuery<FichaActivoDto>;

internal sealed class ObtenerFichaActivoHandler(IAppDbContext db, TimeProvider reloj)
    : IQueryHandler<ObtenerFichaActivoQuery, FichaActivoDto>
{
    public async Task<Result<FichaActivoDto>> Handle(ObtenerFichaActivoQuery query, CancellationToken cancellationToken)
    {
        var activo = await db.Activos
            .AsNoTracking()
            .Include(a => a.Discos)
            .Include(a => a.ModulosRam)
            .FirstOrDefaultAsync(a => a.Id == query.Id, cancellationToken);

        if (activo is null)
        {
            return ErroresActivo.NoEncontrado;
        }

        // Nombres para mostrar. Las tablas son pequeñas y se buscan por clave.
        var sede = await db.Sedes.AsNoTracking().Where(s => s.Id == activo.SedeId).Select(s => s.Nombre).FirstAsync(cancellationToken);
        var establecimiento = await db.Establecimientos.AsNoTracking()
            .Where(e => e.Id == activo.EstablecimientoId)
            .Select(e => new EstablecimientoActivoDto(e.Id, e.CodigoUnico, e.Nombre, e.Categoria, e.Microred, e.Provincia, e.Distrito))
            .FirstAsync(cancellationToken);
        var tipoActivo = await db.TiposActivo.AsNoTracking()
            .Where(t => t.Id == activo.TipoActivoId)
            .Select(t => new { t.Nombre, Ficha = t.FichaAsignada ?? FichaTecnica.Ninguna })
            .FirstAsync(cancellationToken);
        var tipo = tipoActivo.Nombre;
        var marca = await NombreAsync(db.Marcas.AsNoTracking().Where(m => m.Id == activo.MarcaId).Select(m => m.Nombre), cancellationToken);
        var area = await NombreAsync(db.Areas.AsNoTracking().Where(a => a.Id == activo.AreaId).Select(a => a.Nombre), cancellationToken);
        var estado = await NombreAsync(db.EstadosActivo.AsNoTracking().Where(e => e.Id == activo.EstadoActivoId).Select(e => e.Nombre), cancellationToken);
        var sistema = activo.SistemaOperativoId is Guid sistemaId
            ? new ReferenciaDto(
                sistemaId,
                await NombreAsync(db.SistemasOperativos.AsNoTracking().Where(s => s.Id == sistemaId).Select(s => s.Nombre), cancellationToken))
            : null;

        ReferenciaDto? procesador = null;
        OpcionDto? generacion = null;
        if (activo.ProcesadorId is Guid procesadorId)
        {
            var datos = await db.Procesadores.AsNoTracking()
                .Where(p => p.Id == procesadorId)
                .Select(p => new { p.Nombre, p.Generaciones })
                .FirstAsync(cancellationToken);
            procesador = new ReferenciaDto(procesadorId, datos.Nombre);

            // Si el superadministrador cambió las generaciones de la familia, se muestra el código guardado tal cual.
            if (activo.GeneracionProcesador is string codigo)
            {
                var encontrada = GeneracionesDeProcesador.Buscar(datos.Generaciones, codigo);
                generacion = new OpcionDto(codigo, encontrada?.Texto ?? codigo);
            }
        }

        var antivirus = activo.AntivirusId is Guid antivirusId
            ? new ReferenciaDto(
                antivirusId,
                await NombreAsync(db.Antivirus.AsNoTracking().Where(a => a.Id == antivirusId).Select(a => a.Nombre), cancellationToken))
            : null;

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

        return new FichaActivoDto(
            activo.Id,
            tipoActivo.Ficha,
            activo.SedeId,
            sede,
            establecimiento,
            new GeneralesActivoDto(
                activo.CodigoPatrimonial,
                new ReferenciaDto(activo.TipoActivoId, tipo),
                new ReferenciaDto(activo.MarcaId, marca),
                activo.Modelo,
                activo.NumeroSerie,
                activo.Descripcion,
                new ReferenciaDto(activo.AreaId, area),
                activo.UbicacionFisica,
                activo.Responsable,
                new ReferenciaDto(activo.EstadoActivoId, estado),
                activo.Conservacion,
                activo.Criticidad,
                activo.Observaciones),
            new DatosAdquisicionActivo(
                activo.FechaAdquisicion,
                activo.Proveedor,
                activo.NumeroOrdenCompra,
                activo.TipoAdquisicion,
                activo.ValorAdquisicion,
                activo.VidaUtilAnios,
                activo.FinGarantia),
            new TecnicosActivoDto(
                procesador,
                generacion,
                activo.Nucleos,
                activo.TipoGrafica,
                activo.ModeloGrafica,
                activo.PantallaPulgadas,
                sistema,
                activo.VersionSistemaOperativo,
                activo.NombreEquipo,
                activo.DominioGrupo,
                activo.DireccionIp,
                activo.DireccionMac,
                antivirus,
                activo.DiscoCifrado,
                activo.Puertos.Select(p => new OpcionDto(p.ToString(), ListasActivo.Texto(p))).ToList(),
                activo.Accesorios.Select(a => new OpcionDto(a.ToString(), ListasActivo.Texto(a))).ToList()),
            activo.Discos
                .OrderBy(d => d.Orden)
                .Select(d => new DatosDisco(d.Ranura, d.Tipo, d.CapacidadGb, d.MarcaModelo, d.NumeroSerie, d.Uso))
                .ToList(),
            activo.ModulosRam
                .OrderBy(m => m.Orden)
                .Select(m => new DatosModuloRam(m.Ranura, m.Tipo, m.CapacidadGb, m.VelocidadMhz, m.MarcaModelo))
                .ToList(),
            activo.RamTotalGb,
            activo.AlmacenamientoTotalGb,
            activo.Situacion(hoy),
            activo.CreadoEn,
            activo.ModificadoEn);
    }

    /// <summary>Nombre del elemento, o un texto de respaldo si no se encuentra (no debería pasar: hay claves foráneas).</summary>
    private static async Task<string> NombreAsync(IQueryable<string> consulta, CancellationToken cancellationToken) =>
        await consulta.FirstOrDefaultAsync(cancellationToken) ?? "(no disponible)";
}
