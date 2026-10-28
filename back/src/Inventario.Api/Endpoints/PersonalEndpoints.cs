using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Personal;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>
/// Personal de cada red, a quien se le asignan equipos (D50, docs/ARQUITECTURA.md §5.12). Consultar: Activos.Ver.
/// Buscar un DNI en RENIEC, registrar, editar, verificar, cesar y reactivar: Activos.Editar. Cada usuario de sede
/// solo ve y toca el personal de su red.
/// </summary>
internal static class PersonalEndpoints
{
    public static IEndpointRouteBuilder MapPersonalEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/personal").WithTags("Personal");

        grupo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ListarPersonal")
            .WithSummary("Personal de la red por apellidos, con cuántos equipos tiene cada uno. ?buscar= DNI o palabras del nombre (sin importar tildes).");

        // POST y no GET: así el DNI no queda en la dirección ni en los registros de IIS.
        grupo.MapPost("/consulta-dni", ConsultarDniAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("ConsultarDni")
            .WithSummary("Antes de registrar: si el DNI ya está en la red, la persona; si no, lo que responde RENIEC (encontrado, no encontrado o no disponible). No guarda nada.");

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .RequirePermiso(Permisos.Activos.Ver)
            .WithName("ObtenerPersona")
            .WithSummary("Ficha de la persona con los equipos que tiene y tuvo.");

        grupo.MapPost("/", RegistrarAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("RegistrarPersona")
            .WithSummary("Registra a una persona. Los nombres los da RENIEC; los escritos solo se usan si RENIEC no la encuentra o no responde (queda sin verificar).");

        grupo.MapPut("/{id:guid}", EditarAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("EditarPersona")
            .WithSummary("Cambia establecimiento, área y cargo; los nombres solo si no está verificada. El DNI no cambia.");

        grupo.MapPost("/{id:guid}/verificar", VerificarAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("VerificarPersona")
            .WithSummary("Consulta RENIEC otra vez y, si encuentra el DNI, toma sus nombres y marca a la persona como verificada.");

        grupo.MapPost("/{id:guid}/cesar", CesarAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("CesarPersona")
            .WithSummary("Marca a la persona como cesada. Antes debe devolver los equipos que tiene.");

        grupo.MapPost("/{id:guid}/reactivar", ReactivarAsync)
            .RequirePermiso(Permisos.Activos.Editar)
            .WithName("ReactivarPersona")
            .WithSummary("Vuelve a marcar a la persona como activa.");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<PersonaResumenDto>>, ProblemHttpResult>> ListarAsync(
        Guid? sedeId,
        string? buscar,
        Guid? establecimientoId,
        bool? incluirCesados,
        int? maximo,
        IQueryHandler<ListarPersonalQuery, IReadOnlyList<PersonaResumenDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarPersonalQuery(sedeId, buscar, establecimientoId, incluirCesados ?? false, maximo), cancellationToken))
            .ToHttpResult();

    private static async Task<Results<Ok<ConsultaDniDto>, ProblemHttpResult>> ConsultarDniAsync(
        ConsultarDniRequest peticion,
        IQueryHandler<ConsultarDniQuery, ConsultaDniDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ConsultarDniQuery(peticion.Dni, peticion.SedeId), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<PersonaDto>, ProblemHttpResult>> ObtenerAsync(
        Guid id,
        IQueryHandler<ObtenerPersonaQuery, PersonaDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerPersonaQuery(id), cancellationToken)).ToHttpResult();

    private static async Task<Results<Created<IdCreado>, ProblemHttpResult>> RegistrarAsync(
        RegistrarPersonaRequest peticion,
        ICommandHandler<RegistrarPersonaCommand, IdCreado> handler,
        CancellationToken cancellationToken)
    {
        var comando = new RegistrarPersonaCommand(
            peticion.Dni,
            peticion.EstablecimientoId,
            peticion.AreaId,
            peticion.Cargo,
            peticion.ApellidoPaterno,
            peticion.ApellidoMaterno,
            peticion.Nombres);
        return (await handler.Handle(comando, cancellationToken)).ToCreatedResult(c => $"/api/personal/{c.Id}");
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> EditarAsync(
        Guid id,
        EditarPersonaRequest peticion,
        ICommandHandler<EditarPersonaCommand, Vacio> handler,
        CancellationToken cancellationToken)
    {
        var comando = new EditarPersonaCommand(
            id,
            peticion.EstablecimientoId,
            peticion.AreaId,
            peticion.Cargo,
            peticion.ApellidoPaterno,
            peticion.ApellidoMaterno,
            peticion.Nombres);
        return (await handler.Handle(comando, cancellationToken)).ToNoContentResult();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> VerificarAsync(
        Guid id,
        ICommandHandler<VerificarPersonaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new VerificarPersonaCommand(id), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> CesarAsync(
        Guid id,
        ICommandHandler<CesarPersonaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CesarPersonaCommand(id), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> ReactivarAsync(
        Guid id,
        ICommandHandler<ReactivarPersonaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ReactivarPersonaCommand(id), cancellationToken)).ToNoContentResult();
}

/// <summary>DNI a consultar y, para el superadministrador, la red donde se registrará.</summary>
internal sealed record ConsultarDniRequest(string Dni, Guid? SedeId);

/// <summary>Datos para registrar a una persona. Los nombres solo se usan si RENIEC no los da.</summary>
internal sealed record RegistrarPersonaRequest(
    string Dni,
    Guid EstablecimientoId,
    Guid AreaId,
    string? Cargo,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? Nombres);

/// <summary>Datos para editar a una persona. Los nombres solo se usan si no está verificada.</summary>
internal sealed record EditarPersonaRequest(
    Guid EstablecimientoId,
    Guid AreaId,
    string? Cargo,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? Nombres);
