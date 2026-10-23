using Inventario.Domain.Common;
using Inventario.Domain.Seguridad;

namespace Inventario.Domain.Tests;

public sealed class RolTests
{
    private static readonly Guid SedeA = Guid.CreateVersion7();

    [Fact]
    public void Permisos_TienenCodigosUnicos() =>
        Assert.Equal(Permisos.Todos.Count, Permisos.Todos.Select(p => p.Codigo).Distinct().Count());

    [Fact]
    public void Permisos_DeSede_ExcluyeLosExclusivosDelSuperAdmin()
    {
        Assert.DoesNotContain(Permisos.Sedes.Gestionar, Permisos.DeSede);
        Assert.DoesNotContain(Permisos.Administradores.Gestionar, Permisos.DeSede);
        Assert.DoesNotContain(Permisos.Catalogos.GestionarGlobales, Permisos.DeSede);
        Assert.Contains(Permisos.Usuarios.Gestionar, Permisos.DeSede);
    }

    [Fact]
    public void CrearDeSede_ConPermisoExclusivoDelSuperAdmin_Rechaza() =>
        Assert.Throws<DominioException>(() => Rol.CrearDeSede(SedeA, "Técnico", null, [Permisos.Sedes.Gestionar]));

    [Fact]
    public void CrearDeSede_ConPermisoInexistente_Rechaza() =>
        Assert.Throws<DominioException>(() => Rol.CrearDeSede(SedeA, "Técnico", null, ["Inventado.Algo"]));

    [Fact]
    public void CrearDeSede_SinSede_Rechaza() =>
        Assert.Throws<DominioException>(() => Rol.CrearDeSede(Guid.Empty, "Técnico", null, [Permisos.Activos.Ver]));

    [Fact]
    public void DefinirPermisos_IgnoraDuplicadosYReemplazaLosAnteriores()
    {
        var rol = Rol.CrearDeSede(SedeA, "Técnico", null, [Permisos.Activos.Ver, Permisos.Activos.Crear]);

        rol.DefinirPermisos([Permisos.Licencias.Ver, Permisos.Licencias.Ver]);

        var permiso = Assert.Single(rol.PermisosEfectivos());
        Assert.Equal(Permisos.Licencias.Ver, permiso);
    }

    [Fact]
    public void RolesDelSistema_NoSePuedenModificar()
    {
        var administrador = RolesDelSistema.Crear().Single(r => r.Tipo == TipoRol.AdministradorSede);

        Assert.Throws<DominioException>(() => administrador.DefinirPermisos([Permisos.Activos.Ver]));
        Assert.Throws<DominioException>(() => administrador.CambiarDatos("Otro nombre", null));
    }

    [Fact]
    public void SuperAdmin_TieneTodosLosPermisos()
    {
        var superAdmin = RolesDelSistema.Crear().Single(r => r.Tipo == TipoRol.SuperAdmin);

        Assert.Equal(Permisos.Todos.Count, superAdmin.PermisosEfectivos().Count);
    }

    [Fact]
    public void AdministradorDeSede_TieneTodosLosPermisosDeSedeYNingunoExclusivo()
    {
        var administrador = RolesDelSistema.Crear().Single(r => r.Tipo == TipoRol.AdministradorSede);

        Assert.Equal(Permisos.DeSede.Count, administrador.PermisosEfectivos().Count);
        Assert.DoesNotContain(Permisos.Sedes.Gestionar, administrador.PermisosEfectivos());
    }

    [Fact]
    public void RolesIniciales_EditorPuedeRegistrarYConsultaSoloVer()
    {
        var iniciales = RolesDelSistema.CrearInicialesDeSede(SedeA);
        var editor = iniciales.Single(r => r.Nombre == "Editor");
        var consulta = iniciales.Single(r => r.Nombre == "Consulta");

        Assert.Contains(Permisos.Activos.Crear, editor.PermisosEfectivos());
        Assert.DoesNotContain(Permisos.Activos.Eliminar, editor.PermisosEfectivos());
        Assert.All(consulta.PermisosEfectivos(), p => Assert.EndsWith(".Ver", p, StringComparison.Ordinal));
        Assert.All(iniciales, r => Assert.Equal(SedeA, r.SedeId));
    }
}
