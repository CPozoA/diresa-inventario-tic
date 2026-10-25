using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Catalogos;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos;

/// <summary>
/// Ficha técnica completa tal como la envía la interfaz al registrar o editar un activo (docs/ARQUITECTURA.md §5.10).
/// La sede no se envía: es la del establecimiento elegido.
/// </summary>
/// <param name="EstablecimientoId">Establecimiento de la red al que pertenece.</param>
/// <param name="Generales">Identificación, ubicación y estado.</param>
/// <param name="Adquisicion">Adquisición y garantía (opcional).</param>
/// <param name="Tecnicos">Características técnicas (opcional).</param>
/// <param name="Discos">Discos (opcional).</param>
/// <param name="ModulosRam">Módulos de RAM (opcional).</param>
public sealed record FichaActivo(
    Guid EstablecimientoId,
    DatosGeneralesActivo Generales,
    DatosAdquisicionActivo? Adquisicion,
    DatosTecnicosActivo? Tecnicos,
    IReadOnlyList<DatosDisco>? Discos,
    IReadOnlyList<DatosModuloRam>? ModulosRam);

/// <summary>Errores esperados de la gestión de activos.</summary>
public static class ErroresActivo
{
    public static readonly ErrorAplicacion NoEncontrado =
        ErrorAplicacion.NoEncontrado("Activos.NoEncontrado", "El activo no existe.");

    public static readonly ErrorAplicacion FichaIncompleta =
        ErrorAplicacion.Validacion("Activos.FichaIncompleta", "Faltan los datos generales del activo.");

    public static readonly ErrorAplicacion CodigoDuplicado =
        ErrorAplicacion.Conflicto("Activos.CodigoDuplicado", "Ya existe un activo con ese código patrimonial en la sede.");

    public static readonly ErrorAplicacion EstablecimientoInvalido =
        ErrorAplicacion.Validacion("Activos.EstablecimientoInvalido", "El establecimiento no existe o no pertenece a su red.");

    public static readonly ErrorAplicacion OtraRed =
        ErrorAplicacion.ReglaDeNegocio(
            "Activos.OtraRed",
            "El establecimiento es de otra red. Un activo no cambia de red: no existen transferencias entre sedes.");

    public static readonly ErrorAplicacion FechaFutura =
        ErrorAplicacion.Validacion("Activos.FechaFutura", "La fecha de adquisición no puede ser posterior a hoy.");

    public static ErrorAplicacion DatosNoCorresponden(string tipo, FichaTecnica ficha) =>
        ErrorAplicacion.Validacion(
            "Activos.DatosNoCorresponden",
            ficha == FichaTecnica.Red
                ? $"El tipo «{tipo}» solo lleva datos de red (nombre de equipo, IP y MAC): quite las demás características, los discos y la memoria."
                : $"El tipo «{tipo}» no lleva características técnicas, discos ni memoria.");

    public static ErrorAplicacion GeneracionInvalida(string procesador) =>
        ErrorAplicacion.Validacion(
            "Activos.GeneracionInvalida",
            $"La generación elegida no corresponde al procesador «{procesador}». Elija una de su lista.");

    public static ErrorAplicacion ReferenciaInvalida(string campo) =>
        ErrorAplicacion.Validacion("Activos.ReferenciaInvalida", $"{campo} no existe o no está disponible.");

    public static ErrorAplicacion Desactivado(string campo, string nombre) =>
        ErrorAplicacion.Validacion(
            "Activos.ReferenciaDesactivada",
            $"{campo} «{nombre}» está desactivado y ya no se ofrece al registrar. Elija otro.");
}

/// <summary>
/// Comprueba lo que la entidad no puede comprobar sola: que el establecimiento, el área y los elementos de catálogo
/// existan y estén activos, que todo sea de la misma red y que el código patrimonial no se repita en la sede.
/// Un elemento ya desactivado se acepta si el activo ya lo tenía (para poder editar otros datos).
/// </summary>
internal sealed class VerificadorFichaActivo(IAppDbContext db, TimeProvider reloj)
{
    /// <returns>La sede del activo, o el error.</returns>
    public async Task<Result<Guid>> VerificarAsync(FichaActivo ficha, Activo? actual, CancellationToken cancellationToken)
    {
        if (ficha.Generales is null)
        {
            return ErroresActivo.FichaIncompleta;
        }

        // El filtro de sede oculta los establecimientos de otras redes a un usuario de sede.
        var establecimiento = await db.Establecimientos
            .AsNoTracking()
            .Where(e => e.Id == ficha.EstablecimientoId)
            .Select(e => new { e.SedeId, e.Nombre, e.Activo })
            .FirstOrDefaultAsync(cancellationToken);

        if (establecimiento is null)
        {
            return ErroresActivo.EstablecimientoInvalido;
        }

        if (actual is not null && establecimiento.SedeId != actual.SedeId)
        {
            return ErroresActivo.OtraRed;
        }

        if (!establecimiento.Activo && actual?.EstablecimientoId != ficha.EstablecimientoId)
        {
            return ErroresActivo.Desactivado("El establecimiento", establecimiento.Nombre);
        }

        var sedeId = establecimiento.SedeId;
        var g = ficha.Generales;

        // El área debe ser de la misma red que el establecimiento.
        var area = await db.Areas.AsNoTracking()
            .Where(a => a.Id == g.AreaId && a.SedeId == sedeId)
            .Select(a => new Referencia(a.Nombre, a.Activo))
            .FirstOrDefaultAsync(cancellationToken);

        var tipo = await db.TiposActivo.AsNoTracking()
            .Where(t => t.Id == g.TipoActivoId)
            .Select(t => new { t.Nombre, t.Activo, Ficha = t.FichaAsignada ?? FichaTecnica.Ninguna })
            .FirstOrDefaultAsync(cancellationToken);

        var errores = new[]
        {
            Revisar("El área", area, g.AreaId, actual?.AreaId),
            Revisar("El tipo de activo", tipo is null ? null : new Referencia(tipo.Nombre, tipo.Activo), g.TipoActivoId, actual?.TipoActivoId),
            Revisar("La marca", await BuscarAsync(db.Marcas, g.MarcaId, cancellationToken), g.MarcaId, actual?.MarcaId),
            Revisar("El estado", await BuscarAsync(db.EstadosActivo, g.EstadoActivoId, cancellationToken), g.EstadoActivoId, actual?.EstadoActivoId),
        };

        if (errores.FirstOrDefault(e => e is not null) is ErrorAplicacion error)
        {
            return error;
        }

        if (ficha.Tecnicos?.SistemaOperativoId is Guid sistemaId && sistemaId != Guid.Empty)
        {
            var sistema = await BuscarAsync(db.SistemasOperativos, sistemaId, cancellationToken);
            if (Revisar("El sistema operativo", sistema, sistemaId, actual?.SistemaOperativoId) is ErrorAplicacion errorSistema)
            {
                return errorSistema;
            }
        }

        // D44: la generación debe ser de la lista de la familia del procesador.
        if (ficha.Tecnicos?.ProcesadorId is Guid procesadorId && procesadorId != Guid.Empty)
        {
            var procesador = await db.Procesadores.AsNoTracking()
                .Where(p => p.Id == procesadorId)
                .Select(p => new { p.Nombre, p.Activo, p.Generaciones })
                .FirstOrDefaultAsync(cancellationToken);
            var referencia = procesador is null ? null : new Referencia(procesador.Nombre, procesador.Activo);
            if (Revisar("El procesador", referencia, procesadorId, actual?.ProcesadorId) is ErrorAplicacion errorProcesador)
            {
                return errorProcesador;
            }

            var generacion = ficha.Tecnicos.GeneracionProcesador?.Trim();
            if (!string.IsNullOrEmpty(generacion) && GeneracionesDeProcesador.Buscar(procesador!.Generaciones, generacion) is null)
            {
                return ErroresActivo.GeneracionInvalida(procesador.Nombre);
            }
        }

        // D43: solo se aceptan los datos técnicos que lleva el tipo.
        if (!CorrespondeALaFicha(ficha, tipo!.Ficha))
        {
            return ErroresActivo.DatosNoCorresponden(tipo.Nombre, tipo.Ficha);
        }

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (ficha.Adquisicion?.FechaAdquisicion is DateOnly fecha && fecha > hoy)
        {
            return ErroresActivo.FechaFutura;
        }

        // Código único dentro de la sede (se compara ya normalizado, como lo guarda la entidad).
        var codigo = Activo.NormalizarCodigo(g.CodigoPatrimonial);
        var idActual = actual?.Id ?? Guid.Empty;
        if (await db.Activos.AnyAsync(a => a.SedeId == sedeId && a.CodigoPatrimonial == codigo && a.Id != idActual, cancellationToken))
        {
            return ErroresActivo.CodigoDuplicado;
        }

        return sedeId;
    }

    /// <summary>Un tipo de red solo acepta nombre de equipo, IP y MAC; uno sin ficha, nada técnico.</summary>
    private static bool CorrespondeALaFicha(FichaActivo ficha, FichaTecnica tipo)
    {
        if (tipo == FichaTecnica.Computo)
        {
            return true;
        }

        if ((ficha.Discos?.Count ?? 0) > 0 || (ficha.ModulosRam?.Count ?? 0) > 0)
        {
            return false;
        }

        var t = ficha.Tecnicos ?? DatosTecnicosActivo.Vacios;
        var deRed = new[] { t.NombreEquipo, t.DireccionIp, t.DireccionMac };
        var otros = new[]
        {
            t.GeneracionProcesador, t.TarjetaGrafica, t.VersionSistemaOperativo, t.DominioGrupo, t.Antivirus,
            t.PuertosPrincipales, t.AccesoriosIncluidos,
        };

        var tieneOtros = otros.Any(v => !string.IsNullOrWhiteSpace(v))
            || t.Nucleos is not null
            || t.PantallaPulgadas is not null
            || (t.SistemaOperativoId is Guid so && so != Guid.Empty)
            || (t.ProcesadorId is Guid cpu && cpu != Guid.Empty)
            || t.DiscoCifrado is not null;

        return tipo == FichaTecnica.Red
            ? !tieneOtros
            : !tieneOtros && deRed.All(string.IsNullOrWhiteSpace);
    }

    private static Task<Referencia?> BuscarAsync<T>(DbSet<T> tabla, Guid id, CancellationToken cancellationToken)
        where T : ElementoCatalogo =>
        tabla.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new Referencia(e.Nombre, e.Activo))
            .FirstOrDefaultAsync(cancellationToken);

    private static ErrorAplicacion? Revisar(string campo, Referencia? referencia, Guid elegido, Guid? actual)
    {
        if (referencia is null)
        {
            return ErroresActivo.ReferenciaInvalida(campo);
        }

        // Un elemento desactivado no se ofrece para registrar, pero el activo que ya lo tenía puede conservarlo.
        return !referencia.Activo && actual != elegido ? ErroresActivo.Desactivado(campo, referencia.Nombre) : null;
    }

    private sealed record Referencia(string Nombre, bool Activo);
}
