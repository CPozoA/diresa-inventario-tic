using Inventario.Application.Auditoria;
using Inventario.Application.Common.Archivos;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Domain.Activos;
using Inventario.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Application.Activos.Fotos;

/// <summary>Una imagen recibida: su contenido completo (ya acotado al tamaño máximo).</summary>
public sealed record ImagenSubida(byte[] Contenido);

/// <summary>
/// Sube una foto con su miniatura (D38). La primera del activo queda como principal. Se aceptan JPEG, PNG y WebP,
/// reconocidas por su contenido; a las JPEG se les quitan los metadatos (ubicación GPS, celular…).
/// </summary>
/// <param name="ActivoId">Activo.</param>
/// <param name="Foto">La foto (la interfaz la envía reducida a 1600 px).</param>
/// <param name="Miniatura">Versión pequeña para listas; sin ella se usa la misma foto.</param>
/// <param name="Descripcion">Opcional.</param>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record SubirFotoActivoCommand(Guid ActivoId, ImagenSubida? Foto, ImagenSubida? Miniatura, string? Descripcion)
    : ICommand<FotoActivoDto>;

internal sealed class SubirFotoActivoHandler(IAppDbContext db, IAlmacenArchivos almacen, IAuditoria auditoria)
    : ICommandHandler<SubirFotoActivoCommand, FotoActivoDto>
{
    public async Task<Result<FotoActivoDto>> Handle(SubirFotoActivoCommand command, CancellationToken cancellationToken)
    {
        // El filtro de sede oculta los activos de otras redes.
        var activo = await db.Activos.AsNoTracking()
            .Where(a => a.Id == command.ActivoId)
            .Select(a => new { a.Id, a.SedeId, a.CodigoPatrimonial })
            .FirstOrDefaultAsync(cancellationToken);
        if (activo is null)
        {
            return ErroresActivo.NoEncontrado;
        }

        var foto = Preparar(command.Foto, FotoActivo.TamanoMaximoBytes);
        if (foto.Error is ErrorAplicacion errorFoto)
        {
            return errorFoto;
        }

        var miniatura = command.Miniatura is null ? foto : Preparar(command.Miniatura, FotoActivo.TamanoMaximoMiniaturaBytes);
        if (miniatura.Error is ErrorAplicacion errorMiniatura)
        {
            return errorMiniatura;
        }

        // Sin miniatura, la foto sirve como tal solo si es pequeña. La interfaz siempre la envía.
        if (miniatura.Contenido!.Length > FotoActivo.TamanoMaximoMiniaturaBytes)
        {
            return ErroresFoto.SinMiniatura;
        }

        var existentes = await db.FotosActivo
            .Where(f => f.ActivoId == activo.Id)
            .Select(f => new { f.Orden })
            .ToListAsync(cancellationToken);
        if (existentes.Count >= FotoActivo.MaximoPorActivo)
        {
            return ErroresFoto.Maximo;
        }

        var nueva = FotoActivo.Crear(
            activo.SedeId,
            activo.Id,
            existentes.Count == 0 ? 0 : existentes.Max(f => f.Orden) + 1,
            command.Descripcion,
            foto.Tipo,
            foto.Contenido!.Length,
            miniatura.Tipo,
            miniatura.Contenido.Length);
        nueva.MarcarPrincipal(existentes.Count == 0);

        // Primero los archivos; si falla la base de datos, se borran para no dejar huérfanos.
        await almacen.GuardarAsync(nueva.Clave, foto.Contenido, cancellationToken);
        await almacen.GuardarAsync(nueva.ClaveMiniatura, miniatura.Contenido, cancellationToken);
        try
        {
            db.FotosActivo.Add(nueva);
            auditoria.Registrar(ModuloAuditoria.Activos, $"Agregó una foto al activo {activo.CodigoPatrimonial}", activo.SedeId, activo.Id);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await almacen.EliminarAsync(nueva.Clave, CancellationToken.None);
            await almacen.EliminarAsync(nueva.ClaveMiniatura, CancellationToken.None);
            throw;
        }

        return ProyeccionFoto.ADto(nueva);
    }

    private static ImagenPreparada Preparar(ImagenSubida? imagen, int maximo)
    {
        if (imagen is null || imagen.Contenido.Length == 0)
        {
            return new(null, default, ErroresFoto.SinArchivo);
        }

        if (imagen.Contenido.Length > maximo)
        {
            return new(null, default, ErroresFoto.MuyGrande);
        }

        if (ImagenFoto.Detectar(imagen.Contenido) is not TipoImagen tipo)
        {
            return new(null, default, ErroresFoto.TipoNoValido);
        }

        if (tipo != TipoImagen.Jpeg)
        {
            return new(imagen.Contenido, tipo, null);
        }

        var limpia = ImagenFoto.QuitarMetadatosJpeg(imagen.Contenido);
        return limpia is null ? new(null, default, ErroresFoto.Danada) : new(limpia, tipo, null);
    }

    private sealed record ImagenPreparada(byte[]? Contenido, TipoImagen Tipo, ErrorAplicacion? Error);
}

/// <summary>Cambia la descripción de una foto.</summary>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record DescribirFotoActivoCommand(Guid ActivoId, Guid FotoId, string? Descripcion) : ICommand<Vacio>;

internal sealed class DescribirFotoActivoHandler(IAppDbContext db, IAuditoria auditoria) : ICommandHandler<DescribirFotoActivoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(DescribirFotoActivoCommand command, CancellationToken cancellationToken)
    {
        var foto = await db.FotosActivo.FirstOrDefaultAsync(
            f => f.Id == command.FotoId && f.ActivoId == command.ActivoId, cancellationToken);
        if (foto is null)
        {
            return ErroresFoto.NoEncontrada;
        }

        foto.CambiarDescripcion(command.Descripcion);
        auditoria.Registrar(
            ModuloAuditoria.Activos, $"Cambió la descripción de una foto de {await db.CodigoActivoAsync(foto.ActivoId, cancellationToken)}", foto.SedeId, foto.ActivoId);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

/// <summary>Marca una foto como la principal del activo (las demás dejan de serlo).</summary>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record MarcarFotoPrincipalCommand(Guid ActivoId, Guid FotoId) : ICommand<Vacio>;

internal sealed class MarcarFotoPrincipalHandler(IAppDbContext db, IAuditoria auditoria) : ICommandHandler<MarcarFotoPrincipalCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(MarcarFotoPrincipalCommand command, CancellationToken cancellationToken)
    {
        var fotos = await db.FotosActivo.Where(f => f.ActivoId == command.ActivoId).ToListAsync(cancellationToken);
        if (fotos.All(f => f.Id != command.FotoId))
        {
            return ErroresFoto.NoEncontrada;
        }

        foreach (var foto in fotos)
        {
            foto.MarcarPrincipal(foto.Id == command.FotoId);
        }

        var marcada = fotos.First(f => f.Id == command.FotoId);
        auditoria.Registrar(
            ModuloAuditoria.Activos, $"Cambió la foto principal de {await db.CodigoActivoAsync(marcada.ActivoId, cancellationToken)}", marcada.SedeId, marcada.ActivoId);
        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

/// <summary>Elimina una foto y sus archivos. Si era la principal, pasa a serlo la siguiente.</summary>
/// <remarks>Requiere Activos.Editar.</remarks>
public sealed record EliminarFotoActivoCommand(Guid ActivoId, Guid FotoId) : ICommand<Vacio>;

internal sealed class EliminarFotoActivoHandler(IAppDbContext db, IAlmacenArchivos almacen, IAuditoria auditoria)
    : ICommandHandler<EliminarFotoActivoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(EliminarFotoActivoCommand command, CancellationToken cancellationToken)
    {
        var fotos = await db.FotosActivo.Where(f => f.ActivoId == command.ActivoId).ToListAsync(cancellationToken);
        var foto = fotos.FirstOrDefault(f => f.Id == command.FotoId);
        if (foto is null)
        {
            return ErroresFoto.NoEncontrada;
        }

        db.FotosActivo.Remove(foto);
        if (foto.Principal && fotos.Where(f => f.Id != foto.Id).OrderBy(f => f.Orden).FirstOrDefault() is FotoActivo siguiente)
        {
            siguiente.MarcarPrincipal(true);
        }

        auditoria.Registrar(
            ModuloAuditoria.Activos, $"Eliminó una foto de {await db.CodigoActivoAsync(foto.ActivoId, cancellationToken)}", foto.SedeId, foto.ActivoId);
        await db.SaveChangesAsync(cancellationToken);

        // Los archivos se borran después: si fallara, quedaría un archivo sin uso, nunca una foto sin archivo.
        await almacen.EliminarAsync(foto.Clave, cancellationToken);
        await almacen.EliminarAsync(foto.ClaveMiniatura, cancellationToken);
        return Vacio.Valor;
    }
}
