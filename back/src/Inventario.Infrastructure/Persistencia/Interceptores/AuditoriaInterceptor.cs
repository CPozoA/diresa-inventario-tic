using Inventario.Application.Common.Seguridad;
using Inventario.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inventario.Infrastructure.Persistencia.Interceptores;

/// <summary>
/// Rellena quién y cuándo creó o modificó cada <see cref="EntidadAuditable"/> justo antes de guardar.
/// Fuera de una petición HTTP (por ejemplo, al cargar los datos iniciales) el usuario queda vacío: lo hizo el sistema.
/// </summary>
internal sealed class AuditoriaInterceptor(IUsuarioActual usuarioActual, TimeProvider reloj) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Auditar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Auditar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Auditar(DbContext? contexto)
    {
        if (contexto is null)
        {
            return;
        }

        var ahora = reloj.GetUtcNow();
        var usuarioId = usuarioActual.UsuarioId;

        foreach (var entrada in contexto.ChangeTracker.Entries<EntidadAuditable>())
        {
            if (entrada.State == EntityState.Added)
            {
                entrada.Entity.MarcarCreacion(usuarioId, ahora);
            }
            else if (entrada.State == EntityState.Modified)
            {
                entrada.Entity.MarcarModificacion(usuarioId, ahora);
            }
        }
    }
}
