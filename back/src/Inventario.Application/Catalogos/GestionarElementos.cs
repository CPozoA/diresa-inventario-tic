using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Persistencia;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Catalogos;

namespace Inventario.Application.Catalogos;

/// <summary>Agrega un elemento a un catálogo. Nombre único dentro del catálogo (y de la sede, si es de sede).</summary>
/// <remarks>Catálogos de sede: Catalogos.GestionarSede. Generales: Catalogos.GestionarGlobales (solo superadmin).</remarks>
public sealed record CrearElementoCatalogoCommand(string Tipo, Guid? SedeId, string Nombre) : ICommand<IdCreado>;

/// <summary>Cambia el nombre de un elemento.</summary>
public sealed record RenombrarElementoCatalogoCommand(string Tipo, Guid Id, string Nombre) : ICommand<Vacio>;

/// <summary>Activa o desactiva un elemento. Los desactivados ya no se ofrecen al registrar, pero se conservan.</summary>
public sealed record CambiarEstadoElementoCatalogoCommand(string Tipo, Guid Id, bool Activo) : ICommand<Vacio>;

internal sealed class CrearElementoCatalogoHandler(IAppDbContext db, ReglasDeGestion reglas, IPermisosUsuarioActual permisos)
    : ICommandHandler<CrearElementoCatalogoCommand, IdCreado>
{
    public async Task<Result<IdCreado>> Handle(CrearElementoCatalogoCommand command, CancellationToken cancellationToken)
    {
        var definicion = DefinicionesCatalogo.Buscar(command.Tipo);
        if (definicion is null)
        {
            return DefinicionesCatalogo.TipoNoExiste;
        }

        if (!await permisos.TieneAsync(definicion.PermisoGestion, cancellationToken))
        {
            return DefinicionesCatalogo.SinPermiso;
        }

        Guid? sedeId = null;
        if (definicion.EsDeSede)
        {
            var sede = await reglas.ResolverSedeAsync(command.SedeId, cancellationToken);
            if (!sede.EsExito)
            {
                return sede.Error!;
            }

            sedeId = sede.Valor;
        }

        // El dominio valida el nombre (obligatorio, longitud) y quita los espacios sobrantes.
        var elemento = definicion.Agregar(db, sedeId, command.Nombre);

        if (await definicion.ExisteNombreAsync(db, sedeId, elemento.Nombre, excluirId: null, cancellationToken))
        {
            return DefinicionesCatalogo.NombreDuplicado(definicion);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new IdCreado(elemento.Id);
    }
}

internal sealed class RenombrarElementoCatalogoHandler(IAppDbContext db, IPermisosUsuarioActual permisos)
    : ICommandHandler<RenombrarElementoCatalogoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(RenombrarElementoCatalogoCommand command, CancellationToken cancellationToken)
    {
        var buscado = await ElementosCatalogo.BuscarParaGestionAsync(db, permisos, command.Tipo, command.Id, cancellationToken);
        if (!buscado.EsExito)
        {
            return buscado.Error!;
        }

        var (definicion, elemento) = buscado.Valor;
        elemento.CambiarNombre(command.Nombre);

        var sedeId = (elemento as ElementoCatalogoDeSede)?.SedeId;
        if (await definicion.ExisteNombreAsync(db, sedeId, elemento.Nombre, elemento.Id, cancellationToken))
        {
            return DefinicionesCatalogo.NombreDuplicado(definicion);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal sealed class CambiarEstadoElementoCatalogoHandler(IAppDbContext db, IPermisosUsuarioActual permisos)
    : ICommandHandler<CambiarEstadoElementoCatalogoCommand, Vacio>
{
    public async Task<Result<Vacio>> Handle(CambiarEstadoElementoCatalogoCommand command, CancellationToken cancellationToken)
    {
        var buscado = await ElementosCatalogo.BuscarParaGestionAsync(db, permisos, command.Tipo, command.Id, cancellationToken);
        if (!buscado.EsExito)
        {
            return buscado.Error!;
        }

        var elemento = buscado.Valor.Elemento;
        if (command.Activo)
        {
            elemento.Activar();
        }
        else
        {
            elemento.Desactivar();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Vacio.Valor;
    }
}

internal static class ElementosCatalogo
{
    /// <summary>
    /// Busca un elemento para modificarlo, comprobando el permiso del catálogo. En los catálogos de sede, el filtro
    /// de sede hace que un elemento de otra sede "no exista" (404).
    /// </summary>
    public static async Task<Result<(DefinicionCatalogo Definicion, ElementoCatalogo Elemento)>> BuscarParaGestionAsync(
        IAppDbContext db,
        IPermisosUsuarioActual permisos,
        string tipo,
        Guid id,
        CancellationToken cancellationToken)
    {
        var definicion = DefinicionesCatalogo.Buscar(tipo);
        if (definicion is null)
        {
            return DefinicionesCatalogo.TipoNoExiste;
        }

        if (!await permisos.TieneAsync(definicion.PermisoGestion, cancellationToken))
        {
            return DefinicionesCatalogo.SinPermiso;
        }

        var elemento = await definicion.BuscarAsync(db, id, cancellationToken);
        return elemento is null
            ? DefinicionesCatalogo.ElementoNoEncontrado
            : Result.Exito((definicion, elemento));
    }
}
