using Inventario.Domain.Common;
using Inventario.Domain.Personal;
using Inventario.Domain.Seguridad;

namespace Inventario.Domain.Usuarios;

/// <summary>
/// Persona que usa el sistema: sus datos, su sede y sus roles.
/// Las credenciales (nombre de usuario, correo, contraseña, bloqueo) viven aparte, en la cuenta de
/// Identity de la capa Infrastructure, con el mismo identificador. Así el negocio no depende de Identity.
/// </summary>
public sealed class Usuario : EntidadAuditable
{
    public const int LongitudMaximaNombres = 100;
    public const int LongitudMaximaApellidos = 100;
    public const int LongitudMaximaCargo = 100;

    private readonly List<UsuarioRol> _roles = [];

    // Requerido por EF Core.
    private Usuario()
    {
    }

    private Usuario(Guid? sedeId, string? dni, string nombres, string apellidos, string? cargo)
    {
        SedeId = sedeId;
        Dni = dni;
        Nombres = nombres;
        Apellidos = apellidos;
        Cargo = cargo;
        Activo = true;
    }

    /// <summary>Sede a la que pertenece. Solo el superadministrador no tiene sede.</summary>
    public Guid? SedeId { get; private set; }

    /// <summary>
    /// 8 dígitos. Único entre los usuarios. Nulo solo en el superadministrador y en los usuarios creados antes de que
    /// se pidiera (se completa al editarlos); una vez registrado no se puede quitar.
    /// </summary>
    public string? Dni { get; private set; }

    public string Nombres { get; private set; } = string.Empty;

    public string Apellidos { get; private set; } = string.Empty;

    public string? Cargo { get; private set; }

    /// <summary>Un usuario inactivo conserva su historial, pero no puede ingresar.</summary>
    public bool Activo { get; private set; }

    public string NombreCompleto => $"{Nombres} {Apellidos}";

    public IReadOnlyCollection<UsuarioRol> Roles => _roles.AsReadOnly();

    public bool EsSuperAdmin => SedeId is null;

    /// <param name="dni">Application lo exige al crear; el dominio lo acepta nulo por los usuarios anteriores.</param>
    public static Usuario CrearDeSede(Guid sedeId, string nombres, string apellidos, string? cargo, string? dni = null)
    {
        if (sedeId == Guid.Empty)
        {
            throw new DominioException("Un usuario de sede debe indicar su sede.");
        }

        var dniValidado = dni is null ? null : Persona.NormalizarDni(dni);
        return new Usuario(sedeId, dniValidado, ValidarNombres(nombres), ValidarApellidos(apellidos), ValidarCargo(cargo));
    }

    public static Usuario CrearSuperAdmin(string nombres, string apellidos)
    {
        var usuario = new Usuario(sedeId: null, dni: null, ValidarNombres(nombres), ValidarApellidos(apellidos), cargo: null);
        usuario._roles.Add(new UsuarioRol(usuario.Id, RolesDelSistema.SuperAdminId));
        return usuario;
    }

    public void ActualizarDatos(string nombres, string apellidos, string? cargo)
    {
        Nombres = ValidarNombres(nombres);
        Apellidos = ValidarApellidos(apellidos);
        Cargo = ValidarCargo(cargo);
    }

    /// <summary>Registra o corrige el DNI (8 dígitos). Quitarlo no se permite: nulo solo deja el que no tenía.</summary>
    public void CambiarDni(string? dni)
    {
        if (string.IsNullOrWhiteSpace(dni))
        {
            if (Dni is not null)
            {
                throw new DominioException("El DNI no se puede quitar.");
            }

            return;
        }

        Dni = Persona.NormalizarDni(dni);
    }

    /// <summary>
    /// Reemplaza los roles del usuario. Reglas que dependen solo del usuario y de los roles:
    /// al menos un rol; nunca el de superadministrador; los roles de sede deben ser de su misma sede.
    /// Las reglas que dependen de quién asigna (no asignarse a sí mismo, no otorgar permisos que no tiene,
    /// Administrador de sede solo lo asigna el superadministrador) se validan en Application.
    /// </summary>
    public void AsignarRoles(IReadOnlyCollection<Rol> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        if (EsSuperAdmin)
        {
            throw new DominioException("Los roles del superadministrador no se pueden cambiar.");
        }

        if (roles.Count == 0)
        {
            throw new DominioException("Un usuario debe tener al menos un rol.");
        }

        foreach (var rol in roles)
        {
            if (rol.Tipo == TipoRol.SuperAdmin)
            {
                throw new DominioException("El rol Superadministrador no se puede asignar a usuarios de una sede.");
            }

            if (rol.Tipo == TipoRol.DeSede && rol.SedeId != SedeId)
            {
                throw new DominioException($"El rol '{rol.Nombre}' pertenece a otra sede.");
            }
        }

        var nuevos = roles.Select(r => r.Id).ToHashSet();

        _roles.RemoveAll(r => !nuevos.Contains(r.RolId));

        foreach (var rolId in nuevos)
        {
            if (!_roles.Exists(r => r.RolId == rolId))
            {
                _roles.Add(new UsuarioRol(Id, rolId));
            }
        }
    }

    public void Activar() => Activo = true;

    public void Desactivar()
    {
        if (EsSuperAdmin)
        {
            throw new DominioException("El superadministrador no se puede desactivar.");
        }

        Activo = false;
    }

    private static string ValidarNombres(string nombres) =>
        Guardas.TextoObligatorio(nombres, "Los nombres", LongitudMaximaNombres);

    private static string ValidarApellidos(string apellidos) =>
        Guardas.TextoObligatorio(apellidos, "Los apellidos", LongitudMaximaApellidos);

    private static string? ValidarCargo(string? cargo) =>
        Guardas.TextoOpcional(cargo, "El cargo", LongitudMaximaCargo);
}
