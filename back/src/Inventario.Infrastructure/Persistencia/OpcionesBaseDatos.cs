namespace Inventario.Infrastructure.Persistencia;

/// <summary>Sección <c>BaseDatos</c> de appsettings.</summary>
public sealed class OpcionesBaseDatos
{
    public const string Seccion = "BaseDatos";

    /// <summary>
    /// Si es verdadero, la API aplica las migraciones pendientes al arrancar.
    /// Solo para desarrollo: en producción las migraciones se aplican con un script revisado (docs/ARQUITECTURA.md §10).
    /// </summary>
    public bool AplicarMigracionesAlIniciar { get; set; }
}
