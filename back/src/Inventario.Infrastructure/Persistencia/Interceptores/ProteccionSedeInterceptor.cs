using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Common;
using Inventario.Domain.Sedes;
using Inventario.Domain.Seguridad;
using Inventario.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventario.Infrastructure.Persistencia.Interceptores;

/// <summary>
/// Última barrera del aislamiento entre sedes, justo antes de guardar (docs/ARQUITECTURA.md §5.6, punto 4).
/// Aunque un caso de uso tuviera un error, impide que un usuario de sede cree, modifique o borre datos de otra sede,
/// y que cualquier registro cambie de sede (no existen transferencias). Si detecta una violación, cancela todo el guardado.
/// </summary>
internal sealed class ProteccionSedeInterceptor(IUsuarioActual usuarioActual) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Verificar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Verificar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Verificar(DbContext? contexto)
    {
        if (contexto is null || usuarioActual.EsProcesoInterno)
        {
            return;
        }

        var esSuperAdmin = usuarioActual.EsSuperAdmin;
        var sedeUsuario = usuarioActual.SedeId;

        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            if (!TryObtenerSede(entrada.Entity, out var sede))
            {
                continue;
            }

            switch (entrada.State)
            {
                case EntityState.Added:
                case EntityState.Deleted:
                    ExigirMismaSede(esSuperAdmin, sedeUsuario, sede, entrada);
                    break;

                case EntityState.Modified:
                    ExigirSinCambioDeSede(entrada, sede);
                    ExigirMismaSede(esSuperAdmin, sedeUsuario, sede, entrada);
                    break;
            }
        }
    }

    private static void ExigirMismaSede(bool esSuperAdmin, Guid? sedeUsuario, Guid? sedeRegistro, EntityEntry entrada)
    {
        if (esSuperAdmin)
        {
            return;
        }

        // Un usuario de sede solo opera sobre registros de su sede (nunca sobre registros sin sede, como los del sistema).
        if (sedeUsuario is null || sedeRegistro != sedeUsuario)
        {
            throw new ViolacionAislamientoException(
                $"Operación '{entrada.State}' sobre {entrada.Metadata.ClrType.Name} de otra sede o sin sede.");
        }
    }

    private static void ExigirSinCambioDeSede(EntityEntry entrada, Guid? sedeActual)
    {
        if (entrada.Entity is Sede)
        {
            return;
        }

        var original = (Guid?)entrada.Property("SedeId").OriginalValue;
        if (original != sedeActual)
        {
            throw new ViolacionAislamientoException(
                $"Se intentó cambiar la sede de un registro de {entrada.Metadata.ClrType.Name}. No existen transferencias entre sedes.");
        }
    }

    private static bool TryObtenerSede(object entidad, out Guid? sede)
    {
        switch (entidad)
        {
            case IPerteneceASede registro:
                sede = registro.SedeId;
                return true;
            case Usuario usuario:
                sede = usuario.SedeId;
                return true;
            case Rol rol:
                sede = rol.SedeId;
                return true;
            case Sede s:
                sede = s.Id;
                return true;
            default:
                sede = null;
                return false;
        }
    }
}

/// <summary>
/// Se intentó romper el aislamiento entre sedes. No es un error esperado: indica un defecto o un intento de abuso,
/// así que sale como error 500 genérico y queda registrado en el log.
/// </summary>
public sealed class ViolacionAislamientoException : Exception
{
    public ViolacionAislamientoException()
    {
    }

    public ViolacionAislamientoException(string message)
        : base(message)
    {
    }

    public ViolacionAislamientoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
