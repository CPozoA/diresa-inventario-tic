using Inventario.Api.IntegrationTests.Infraestructura;
using Inventario.Domain.Seguridad;
using Inventario.Infrastructure.Persistencia.Interceptores;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.IntegrationTests;

/// <summary>
/// Pruebas obligatorias de aislamiento (docs/ARQUITECTURA.md §8): con dos sedes, Huanta y Puquio,
/// un usuario de Huanta no ve ni puede modificar nada de Puquio. Usan el filtro y la protección reales de EF Core.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public sealed class AislamientoEntreSedesTests(FabricaApi api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UsuarioDeHuanta_SoloVeUsuariosDeHuanta()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.DeSede(api.SedeHuantaId));

        var usuarios = await db.Usuarios.ToListAsync(Ct);

        Assert.NotEmpty(usuarios);
        Assert.All(usuarios, u => Assert.Equal(api.SedeHuantaId, u.SedeId));
    }

    [Fact]
    public async Task UsuarioDeHuanta_NoVeAlSuperAdministrador()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.DeSede(api.SedeHuantaId));

        Assert.False(await db.Usuarios.AnyAsync(u => u.SedeId == null, Ct));
    }

    [Fact]
    public async Task UsuarioDeHuanta_BuscarPorIdUnUsuarioDePuquio_NoLoEncuentra()
    {
        Guid idDePuquio;
        await using (var interno = FabricaApi.CrearContexto(UsuarioFalso.ProcesoInterno()))
        {
            idDePuquio = await interno.Usuarios.Where(u => u.SedeId == api.SedePuquioId).Select(u => u.Id).FirstAsync(Ct);
        }

        await using var db = FabricaApi.CrearContexto(UsuarioFalso.DeSede(api.SedeHuantaId));

        Assert.Null(await db.Usuarios.FirstOrDefaultAsync(u => u.Id == idDePuquio, Ct));
    }

    [Fact]
    public async Task UsuarioDeHuanta_SoloVeSuPropiaSede()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.DeSede(api.SedeHuantaId));

        var sede = Assert.Single(await db.Sedes.ToListAsync(Ct));
        Assert.Equal(api.SedeHuantaId, sede.Id);
    }

    [Fact]
    public async Task UsuarioDeHuanta_VeRolesDeSuSedeYDelSistemaPeroNoDePuquio()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.DeSede(api.SedeHuantaId));

        var roles = await db.Roles.ToListAsync(Ct);

        Assert.Contains(roles, r => r.SedeId == api.SedeHuantaId);
        Assert.Contains(roles, r => r.Tipo == TipoRol.AdministradorSede);
        Assert.DoesNotContain(roles, r => r.SedeId == api.SedePuquioId);
    }

    [Fact]
    public async Task UsuarioDeHuanta_CrearUnRolEnPuquio_SeRechazaAlGuardar()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.DeSede(api.SedeHuantaId));

        db.Roles.Add(Rol.CrearDeSede(api.SedePuquioId, "Intruso", null, [Permisos.Activos.Ver]));

        await Assert.ThrowsAsync<ViolacionAislamientoException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task UsuarioDeHuanta_ModificarUnRolDelSistema_SeRechazaAlGuardar()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.DeSede(api.SedeHuantaId));
        var administrador = await db.Roles.SingleAsync(r => r.Tipo == TipoRol.AdministradorSede, Ct);

        db.Entry(administrador).Property(r => r.Nombre).CurrentValue = "Administrador modificado";

        await Assert.ThrowsAsync<ViolacionAislamientoException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task UsuarioDeHuanta_PasarUnRolSuyoAPuquio_SeRechazaAlGuardar()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.DeSede(api.SedeHuantaId));
        var rol = await db.Roles.SingleAsync(r => r.SedeId == api.SedeHuantaId && r.Nombre == DatosPrueba.RolTecnico, Ct);

        db.Entry(rol).Property(r => r.SedeId).CurrentValue = api.SedePuquioId;

        await Assert.ThrowsAsync<ViolacionAislamientoException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task SuperAdministrador_TampocoPuedeCambiarUnRegistroDeSede()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.SuperAdministrador());
        var rol = await db.Roles.SingleAsync(r => r.SedeId == api.SedeHuantaId && r.Nombre == DatosPrueba.RolTecnico, Ct);

        db.Entry(rol).Property(r => r.SedeId).CurrentValue = api.SedePuquioId;

        await Assert.ThrowsAsync<ViolacionAislamientoException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task SuperAdministrador_VeTodasLasSedesYUsuarios()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.SuperAdministrador());

        Assert.Equal(7, await db.Sedes.CountAsync(Ct));
        Assert.True(await db.Usuarios.AnyAsync(u => u.SedeId == api.SedeHuantaId, Ct));
        Assert.True(await db.Usuarios.AnyAsync(u => u.SedeId == api.SedePuquioId, Ct));
    }

    [Fact]
    public async Task PeticionAnonima_NoVeNada()
    {
        await using var db = FabricaApi.CrearContexto(UsuarioFalso.Anonimo());

        Assert.False(await db.Sedes.AnyAsync(Ct));
        Assert.False(await db.Usuarios.AnyAsync(Ct));
        Assert.False(await db.Roles.AnyAsync(Ct));
    }
}
