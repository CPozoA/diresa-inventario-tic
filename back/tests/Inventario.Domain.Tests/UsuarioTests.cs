using Inventario.Domain.Common;
using Inventario.Domain.Seguridad;
using Inventario.Domain.Usuarios;

namespace Inventario.Domain.Tests;

public sealed class UsuarioTests
{
    private static readonly Guid SedeA = Guid.CreateVersion7();
    private static readonly Guid SedeB = Guid.CreateVersion7();

    private static readonly IReadOnlyList<Rol> Sistema = RolesDelSistema.Crear();
    private static Rol SuperAdmin => Sistema.Single(r => r.Tipo == TipoRol.SuperAdmin);
    private static Rol AdministradorSede => Sistema.Single(r => r.Tipo == TipoRol.AdministradorSede);

    [Fact]
    public void AsignarRoles_PermiteVariosRolesDeSuSedeYAdministradorDeSede()
    {
        var iniciales = RolesDelSistema.CrearInicialesDeSede(SedeA);
        var usuario = Usuario.CrearDeSede(SedeA, "María", "Quispe", null);

        usuario.AsignarRoles([.. iniciales, AdministradorSede]);

        Assert.Equal(3, usuario.Roles.Count);
    }

    [Fact]
    public void AsignarRoles_ReemplazaLosRolesAnteriores()
    {
        var iniciales = RolesDelSistema.CrearInicialesDeSede(SedeA);
        var usuario = Usuario.CrearDeSede(SedeA, "María", "Quispe", null);
        usuario.AsignarRoles(iniciales);

        usuario.AsignarRoles([iniciales[1]]);

        var rol = Assert.Single(usuario.Roles);
        Assert.Equal(iniciales[1].Id, rol.RolId);
    }

    [Fact]
    public void AsignarRoles_ConRolDeOtraSede_Rechaza()
    {
        var rolDeOtraSede = Rol.CrearDeSede(SedeB, "Técnico", null, [Permisos.Activos.Ver]);
        var usuario = Usuario.CrearDeSede(SedeA, "María", "Quispe", null);

        Assert.Throws<DominioException>(() => usuario.AsignarRoles([rolDeOtraSede]));
    }

    [Fact]
    public void AsignarRoles_ConRolSuperAdmin_Rechaza()
    {
        var usuario = Usuario.CrearDeSede(SedeA, "María", "Quispe", null);

        Assert.Throws<DominioException>(() => usuario.AsignarRoles([SuperAdmin]));
    }

    [Fact]
    public void AsignarRoles_SinRoles_Rechaza()
    {
        var usuario = Usuario.CrearDeSede(SedeA, "María", "Quispe", null);

        Assert.Throws<DominioException>(() => usuario.AsignarRoles([]));
    }

    [Fact]
    public void CrearDeSede_SinSede_Rechaza() =>
        Assert.Throws<DominioException>(() => Usuario.CrearDeSede(Guid.Empty, "María", "Quispe", null));

    [Fact]
    public void SuperAdmin_NoTieneSedeTieneSuRolYNoSePuedeCambiarNiDesactivar()
    {
        var superAdmin = Usuario.CrearSuperAdmin("Cristian", "Pozo");

        Assert.True(superAdmin.EsSuperAdmin);
        Assert.Null(superAdmin.SedeId);
        Assert.Equal(RolesDelSistema.SuperAdminId, Assert.Single(superAdmin.Roles).RolId);
        Assert.Throws<DominioException>(() => superAdmin.AsignarRoles([AdministradorSede]));
        Assert.Throws<DominioException>(superAdmin.Desactivar);
    }

    [Fact]
    public void UsuarioDeSede_SePuedeDesactivarYActivar()
    {
        var usuario = Usuario.CrearDeSede(SedeA, "María", "Quispe", null);

        usuario.Desactivar();
        Assert.False(usuario.Activo);

        usuario.Activar();
        Assert.True(usuario.Activo);
    }
}
