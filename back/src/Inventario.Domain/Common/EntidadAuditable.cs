namespace Inventario.Domain.Common;

/// <summary>
/// Entidad que registra quién y cuándo la creó y la modificó por última vez.
/// Estos campos los rellena automáticamente la infraestructura al guardar (nunca el código de negocio).
/// </summary>
public abstract class EntidadAuditable : Entidad
{
    protected EntidadAuditable()
    {
    }

    protected EntidadAuditable(Guid id)
        : base(id)
    {
    }

    public DateTimeOffset CreadoEn { get; private set; }

    public Guid? CreadoPor { get; private set; }

    public DateTimeOffset? ModificadoEn { get; private set; }

    public Guid? ModificadoPor { get; private set; }

    public void MarcarCreacion(Guid? usuarioId, DateTimeOffset fecha)
    {
        CreadoPor = usuarioId;
        CreadoEn = fecha;
    }

    public void MarcarModificacion(Guid? usuarioId, DateTimeOffset fecha)
    {
        ModificadoPor = usuarioId;
        ModificadoEn = fecha;
    }
}
