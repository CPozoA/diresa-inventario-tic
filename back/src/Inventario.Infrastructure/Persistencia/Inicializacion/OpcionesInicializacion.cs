namespace Inventario.Infrastructure.Persistencia.Inicializacion;

/// <summary>Sección <c>Inicializacion</c> de appsettings.</summary>
public sealed class OpcionesInicializacion
{
    public const string Seccion = "Inicializacion";

    public OpcionesSuperAdmin SuperAdmin { get; set; } = new();
}

/// <summary>
/// Datos para crear la cuenta del superadministrador la primera vez que arranca el sistema.
/// La contraseña inicial es temporal (se exige cambiarla al entrar) y nunca va en el repositorio:
/// en desarrollo se guarda con <c>dotnet user-secrets</c> y en el servidor, como variable de entorno.
/// </summary>
public sealed class OpcionesSuperAdmin
{
    public string NombreUsuario { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public string Nombres { get; set; } = "Superadministrador";

    public string Apellidos { get; set; } = "del sistema";

    public string ContrasenaInicial { get; set; } = string.Empty;

    public bool EstaCompleto =>
        !string.IsNullOrWhiteSpace(NombreUsuario)
        && !string.IsNullOrWhiteSpace(Correo)
        && !string.IsNullOrWhiteSpace(ContrasenaInicial);
}
