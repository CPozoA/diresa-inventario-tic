using Inventario.Application.Common.Resultados;

namespace Inventario.Application.Common.Mensajeria;

/// <summary>Comando: una intención de cambiar datos (crear, editar, eliminar).</summary>
/// <typeparam name="TResultado">Lo que devuelve si tiene éxito (por ejemplo, el identificador creado).</typeparam>
#pragma warning disable CA1040 // Interfaz marcadora intencional: tipa comandos y consultas.
public interface ICommand<TResultado>
{
}

/// <summary>Consulta: solo lee datos, nunca los cambia.</summary>
/// <typeparam name="TResultado">El dato o DTO que devuelve.</typeparam>
public interface IQuery<TResultado>
{
}
#pragma warning restore CA1040

/// <summary>Ejecuta un comando. Hay exactamente un handler por comando.</summary>
public interface ICommandHandler<in TCommand, TResultado>
    where TCommand : ICommand<TResultado>
{
    Task<Result<TResultado>> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Ejecuta una consulta. Hay exactamente un handler por consulta.</summary>
public interface IQueryHandler<in TQuery, TResultado>
    where TQuery : IQuery<TResultado>
{
    Task<Result<TResultado>> Handle(TQuery query, CancellationToken cancellationToken);
}
