using System.Globalization;
using System.Security.Claims;
using Inventario.Application.Common.Seguridad;
using Inventario.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventario.Infrastructure.Identidad;

/// <summary>
/// Arma los datos (claims) que guarda la cookie de sesión. Además del identificador, el nombre y el sello de seguridad
/// que agrega Identity, incluye la sede, si es superadministrador y si debe cambiar su contraseña.
/// Los permisos NO van en la cookie: se calculan en el servidor en cada petición (Paso 4).
/// </summary>
internal sealed class FabricaPrincipal(
    UserManager<CuentaUsuario> userManager,
    IOptions<IdentityOptions> optionsAccessor,
    AppDbContext db)
    : UserClaimsPrincipalFactory<CuentaUsuario>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(CuentaUsuario user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identidad = await base.GenerateClaimsAsync(user);

        // Excepción revisada al filtro de sede: se arma la sesión del propio usuario, a veces durante una petición aún anónima.
        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Id == user.Id)
            .Select(u => new { u.SedeId })
            .FirstOrDefaultAsync();

        if (usuario?.SedeId is Guid sedeId)
        {
            identidad.AddClaim(new Claim(TiposClaim.Sede, sedeId.ToString("D", CultureInfo.InvariantCulture)));
        }
        else if (usuario is not null)
        {
            // Solo el superadministrador no tiene sede (regla del dominio).
            identidad.AddClaim(new Claim(TiposClaim.SuperAdmin, bool.TrueString));
        }

        if (user.DebeCambiarContrasena)
        {
            identidad.AddClaim(new Claim(TiposClaim.DebeCambiarContrasena, bool.TrueString));
        }

        return identidad;
    }
}
