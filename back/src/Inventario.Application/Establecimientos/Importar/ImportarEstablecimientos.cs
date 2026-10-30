using System.Globalization;
using Inventario.Application.Auditoria;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Auditoria;
using Inventario.Domain.Common;
using Inventario.Domain.Establecimientos;
using Inventario.Domain.Sedes;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Establecimientos.Importar;

/// <summary>Problema en una fila del Excel.</summary>
public sealed record ErrorFilaDto(int Fila, string Mensaje);

/// <summary>
/// Resultado de una importación. Si hay errores, <see cref="Guardado"/> es falso y no se cambió nada:
/// se corrigen las filas indicadas y se vuelve a importar.
/// </summary>
public sealed record ResultadoImportacionDto(
    bool Guardado,
    int Filas,
    int Creados,
    int Actualizados,
    int SinCambios,
    IReadOnlyList<ErrorFilaDto> Errores);

/// <summary>
/// Importa establecimientos desde Excel (docs/ARQUITECTURA.md §5.9). Se revisan todas las filas antes de guardar;
/// si alguna tiene errores no se guarda nada. Una fila con un Id_Establecimiento que ya existe actualiza sus datos,
/// así se puede volver a importar la lista corregida o ampliada.
/// </summary>
/// <remarks>Requiere Establecimientos.Gestionar (solo el superadministrador).</remarks>
public sealed record ImportarEstablecimientosCommand(Stream? Archivo, long Tamano) : ICommand<ResultadoImportacionDto>;

internal sealed class ImportarEstablecimientosHandler(IAppDbContext db, IHojaEstablecimientos hoja, IAuditoria auditoria)
    : ICommandHandler<ImportarEstablecimientosCommand, ResultadoImportacionDto>
{
    public const int TamanoMaximoMb = 5;
    public const int FilasMaximas = 5000;

    public async Task<Result<ResultadoImportacionDto>> Handle(
        ImportarEstablecimientosCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Archivo is null || command.Tamano <= 0)
        {
            return ErroresEstablecimiento.ArchivoVacio;
        }

        if (command.Tamano > TamanoMaximoMb * 1024L * 1024L)
        {
            return ErroresEstablecimiento.ArchivoMuyGrande;
        }

        var lectura = hoja.Leer(command.Archivo);
        if (lectura.Error is not null)
        {
            return ErroresEstablecimiento.ArchivoInvalido(lectura.Error);
        }

        if (lectura.Filas.Count == 0)
        {
            return ErroresEstablecimiento.ArchivoInvalido("El archivo no tiene filas con establecimientos debajo de los encabezados.");
        }

        if (lectura.Filas.Count > FilasMaximas)
        {
            return ErroresEstablecimiento.ArchivoInvalido(
                $"El archivo tiene {lectura.Filas.Count} filas; el máximo por importación es {FilasMaximas}.");
        }

        // Solo el superadministrador importa: ve todas las sedes y todos los establecimientos.
        var sedes = await db.Sedes.AsNoTracking().ToListAsync(cancellationToken);
        var existentes = await db.Establecimientos.ToDictionaryAsync(e => e.IdEstablecimiento, cancellationToken);

        var revision = new RevisionImportacion(sedes, existentes);
        foreach (var fila in lectura.Filas)
        {
            revision.Revisar(fila);
        }

        revision.VerificarCodigosUnicos();

        if (revision.Errores.Count > 0)
        {
            var errores = revision.Errores.OrderBy(e => e.Fila).ToList();
            return new ResultadoImportacionDto(false, lectura.Filas.Count, 0, 0, 0, errores);
        }

        var creados = 0;
        var actualizados = 0;
        var sinCambios = 0;

        foreach (var valida in revision.Validas)
        {
            if (valida.Existente is Establecimiento existente)
            {
                if (existente.Actualizar(valida.Datos))
                {
                    actualizados++;
                }
                else
                {
                    sinCambios++;
                }
            }
            else
            {
                db.Establecimientos.Add(Establecimiento.Crear(valida.SedeId, valida.Datos));
                creados++;
            }
        }

        auditoria.Registrar(
            ModuloAuditoria.Establecimientos,
            $"Importó establecimientos desde Excel: {creados} creados, {actualizados} actualizados y {sinCambios} sin cambios",
            sedeId: null);
        await db.SaveChangesAsync(cancellationToken);

        return new ResultadoImportacionDto(true, lectura.Filas.Count, creados, actualizados, sinCambios, []);
    }

    /// <summary>Revisa las filas sin tocar la base de datos ni las entidades existentes.</summary>
    private sealed class RevisionImportacion(IReadOnlyList<Sede> sedes, Dictionary<int, Establecimiento> existentes)
    {
        private readonly HashSet<int> _idsEnArchivo = [];

        private readonly string _sedesValidas = string.Join(", ", sedes.Select(s => s.Nombre));

        public List<ErrorFilaDto> Errores { get; } = [];

        public List<FilaValida> Validas { get; } = [];

        public void Revisar(FilaEstablecimiento fila)
        {
            var erroresAntes = Errores.Count;

            var id = LeerNumero(fila, fila.IdEstablecimiento, "Id_Establecimiento");
            var codigo = LeerNumero(fila, fila.CodigoUnico, "Codigo_Unico");
            var sede = ReconocedorRed.Buscar(fila.Red, sedes);

            if (string.IsNullOrWhiteSpace(fila.Red))
            {
                Agregar(fila, "Falta la red.");
            }
            else if (sede is null)
            {
                Agregar(fila, $"No se reconoce la red «{fila.Red.Trim()}». Debe corresponder a una de las sedes: {_sedesValidas}.");
            }

            if (id is int idRepetido && !_idsEnArchivo.Add(idRepetido))
            {
                Agregar(fila, $"El Id_Establecimiento {idRepetido} se repite en el archivo.");
            }

            if (Errores.Count > erroresAntes || id is not int idEstablecimiento || codigo is not int codigoUnico || sede is null)
            {
                return;
            }

            var datos = new DatosEstablecimiento(
                idEstablecimiento,
                codigoUnico,
                fila.Nombre ?? string.Empty,
                fila.Categoria,
                fila.Microred,
                fila.Provincia,
                fila.Distrito,
                CompletarUbigeo(fila.Ubigeo));

            try
            {
                // Valida con las mismas reglas del dominio, sobre una copia descartable.
                _ = Establecimiento.Crear(sede.Id, datos);
            }
            catch (DominioException ex)
            {
                Agregar(fila, ex.Message);
                return;
            }

            var existente = existentes.GetValueOrDefault(idEstablecimiento);
            if (existente is not null && existente.SedeId != sede.Id)
            {
                Agregar(fila, "El establecimiento ya está registrado en otra red. No se puede cambiar de red.");
                return;
            }

            Validas.Add(new FilaValida(fila.NumeroFila, sede.Id, datos, existente));
        }

        /// <summary>
        /// El código único no se repite. Se compara el estado final: los establecimientos que no vienen en el archivo
        /// conservan su código, y los del archivo toman el nuevo.
        /// </summary>
        public void VerificarCodigosUnicos()
        {
            var idsDelArchivo = Validas.Select(v => v.Datos.IdEstablecimiento).ToHashSet();
            var codigosFueraDelArchivo = existentes.Values
                .Where(e => !idsDelArchivo.Contains(e.IdEstablecimiento))
                .ToDictionary(e => e.CodigoUnico, e => e.IdEstablecimiento);

            var vistos = new Dictionary<int, int>();
            foreach (var valida in Validas)
            {
                var codigo = valida.Datos.CodigoUnico;

                if (codigosFueraDelArchivo.TryGetValue(codigo, out var otroId))
                {
                    Errores.Add(new ErrorFilaDto(
                        valida.Fila,
                        $"El código único {codigo} ya pertenece al establecimiento con Id_Establecimiento {otroId}."));
                }
                else if (!vistos.TryAdd(codigo, valida.Fila))
                {
                    Errores.Add(new ErrorFilaDto(
                        valida.Fila,
                        $"El código único {codigo} se repite en el archivo (también en la fila {vistos[codigo]})."));
                }
            }
        }

        /// <summary>Un ubigeo numérico pierde el 0 inicial en Excel (50601 en vez de 050601): se completa.</summary>
        private static string? CompletarUbigeo(string? ubigeo)
        {
            var limpio = ubigeo?.Trim();
            return !string.IsNullOrEmpty(limpio) && limpio.Length < 6 && limpio.All(char.IsAsciiDigit)
                ? limpio.PadLeft(6, '0')
                : limpio;
        }

        private int? LeerNumero(FilaEstablecimiento fila, string? texto, string columna)
        {
            var limpio = texto?.Trim();
            if (string.IsNullOrEmpty(limpio))
            {
                Agregar(fila, $"Falta {columna}.");
                return null;
            }

            if (!int.TryParse(limpio, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) || numero <= 0)
            {
                Agregar(fila, $"{columna} debe ser un número entero mayor que cero (se leyó «{limpio}»).");
                return null;
            }

            return numero;
        }

        private void Agregar(FilaEstablecimiento fila, string mensaje) => Errores.Add(new ErrorFilaDto(fila.NumeroFila, mensaje));
    }

    private sealed record FilaValida(int Fila, Guid SedeId, DatosEstablecimiento Datos, Establecimiento? Existente);
}
