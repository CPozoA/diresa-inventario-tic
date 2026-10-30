using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Licencias;
using Inventario.Domain.Licencias;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>
/// Licencias de software (D54, docs/ARQUITECTURA.md §5.14): suscripciones con puestos que se asignan a equipos.
/// Consultar: Licencias.Ver. Registrar, modificar y asignar: Licencias.Gestionar. Cada red solo ve las suyas.
/// </summary>
internal static class LicenciasEndpoints
{
    public static IEndpointRouteBuilder MapLicenciasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/licencias").WithTags("Licencias");

        grupo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Licencias.Ver)
            .WithName("ListarLicencias")
            .WithSummary("Licencias de la red, la que vence antes primero. ?filtro= Vigentes, PorVencer, Vencidas, Excedidas o Archivadas (sin filtro, todas menos las archivadas). ?buscar= producto, proveedor, documento o código de un equipo.");

        grupo.MapGet("/cifras", CifrasAsync)
            .RequirePermiso(Permisos.Licencias.Ver)
            .WithName("ObtenerCifrasLicencias")
            .WithSummary("Vigentes, por vencer (30 días), vencidas, excedidas y puestos libres. No cuentan las archivadas.");

        grupo.MapGet("/productos", ProductosAsync)
            .RequirePermiso(Permisos.Licencias.Gestionar)
            .WithName("ListarProductosLicencia")
            .WithSummary("Productos ya registrados, para sugerirlos al escribir.");

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .RequirePermiso(Permisos.Licencias.Ver)
            .WithName("ObtenerLicencia")
            .WithSummary("Ficha de la licencia con sus equipos.");

        grupo.MapPost("/", RegistrarAsync)
            .RequirePermiso(Permisos.Licencias.Gestionar)
            .WithName("RegistrarLicencia")
            .WithSummary("Registra una suscripción. El superadministrador indica sedeId. No se guarda la clave del producto.");

        grupo.MapPut("/{id:guid}", ActualizarAsync)
            .RequirePermiso(Permisos.Licencias.Gestionar)
            .WithName("ActualizarLicencia")
            .WithSummary("Modifica la licencia; también para renovarla (nuevas fechas).");

        grupo.MapDelete("/{id:guid}", EliminarAsync)
            .RequirePermiso(Permisos.Licencias.Gestionar)
            .WithName("EliminarLicencia")
            .WithSummary("Elimina una licencia registrada por error, con sus equipos.");

        grupo.MapPost("/{id:guid}/archivar", ArchivarAsync)
            .RequirePermiso(Permisos.Licencias.Gestionar)
            .WithName("ArchivarLicencia")
            .WithSummary("La licencia no se renovó: deja de contar en las alertas y libera sus equipos.");

        grupo.MapPost("/{id:guid}/reactivar", ReactivarAsync)
            .RequirePermiso(Permisos.Licencias.Gestionar)
            .WithName("ReactivarLicencia")
            .WithSummary("Vuelve a activar una licencia archivada.");

        grupo.MapPost("/{id:guid}/equipos", AsignarAsync)
            .RequirePermiso(Permisos.Licencias.Gestionar)
            .WithName("AsignarLicencia")
            .WithSummary("Asigna puestos a equipos de la red (no de baja). Todos o ninguno.");

        grupo.MapDelete("/{id:guid}/equipos/{activoId:guid}", QuitarAsync)
            .RequirePermiso(Permisos.Licencias.Gestionar)
            .WithName("QuitarLicencia")
            .WithSummary("Libera el puesto del equipo.");

        app.MapGet("/activos/{id:guid}/licencias", DelActivoAsync)
            .WithTags("Licencias")
            .RequirePermiso(Permisos.Licencias.Ver)
            .WithName("ObtenerLicenciasActivo")
            .WithSummary("Licencias (no archivadas) instaladas en el activo.");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<LicenciaDto>>, ProblemHttpResult>> ListarAsync(
        Guid? sedeId,
        string? buscar,
        FiltroLicencias? filtro,
        IQueryHandler<ListarLicenciasQuery, IReadOnlyList<LicenciaDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarLicenciasQuery(sedeId, buscar, filtro), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<CifrasLicenciasDto>, ProblemHttpResult>> CifrasAsync(
        Guid? sedeId,
        IQueryHandler<ObtenerCifrasLicenciasQuery, CifrasLicenciasDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerCifrasLicenciasQuery(sedeId), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<IReadOnlyList<string>>, ProblemHttpResult>> ProductosAsync(
        IQueryHandler<ListarProductosLicenciaQuery, IReadOnlyList<string>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarProductosLicenciaQuery(), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<FichaLicenciaDto>, ProblemHttpResult>> ObtenerAsync(
        Guid id,
        IQueryHandler<ObtenerLicenciaQuery, FichaLicenciaDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerLicenciaQuery(id), cancellationToken)).ToHttpResult();

    private static async Task<Results<Created<IdCreado>, ProblemHttpResult>> RegistrarAsync(
        LicenciaRequest peticion,
        ICommandHandler<RegistrarLicenciaCommand, IdCreado> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new RegistrarLicenciaCommand(peticion.SedeId, peticion.ADatos()), cancellationToken))
            .ToCreatedResult(c => $"/api/licencias/{c.Id}");

    private static async Task<Results<NoContent, ProblemHttpResult>> ActualizarAsync(
        Guid id,
        LicenciaRequest peticion,
        ICommandHandler<ActualizarLicenciaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ActualizarLicenciaCommand(id, peticion.ADatos()), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> EliminarAsync(
        Guid id,
        ICommandHandler<EliminarLicenciaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new EliminarLicenciaCommand(id), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> ArchivarAsync(
        Guid id,
        ICommandHandler<ArchivarLicenciaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ArchivarLicenciaCommand(id), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> ReactivarAsync(
        Guid id,
        ICommandHandler<ReactivarLicenciaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ReactivarLicenciaCommand(id), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> AsignarAsync(
        Guid id,
        AsignarLicenciaRequest peticion,
        ICommandHandler<AsignarLicenciaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new AsignarLicenciaCommand(id, peticion.ActivoIds), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> QuitarAsync(
        Guid id,
        Guid activoId,
        ICommandHandler<QuitarLicenciaCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new QuitarLicenciaCommand(id, activoId), cancellationToken)).ToNoContentResult();

    private static async Task<Results<Ok<IReadOnlyList<LicenciaDelActivoDto>>, ProblemHttpResult>> DelActivoAsync(
        Guid id,
        IQueryHandler<ObtenerLicenciasActivoQuery, IReadOnlyList<LicenciaDelActivoDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerLicenciasActivoQuery(id), cancellationToken)).ToHttpResult();
}

/// <summary>Datos de una licencia. <c>sedeId</c> solo al registrar y solo el superadministrador.</summary>
internal sealed record LicenciaRequest(
    Guid? SedeId,
    string Producto,
    string? Proveedor,
    int Puestos,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? DocumentoCompra,
    decimal? Costo,
    string? Observaciones)
{
    public DatosLicencia ADatos() =>
        new(Producto, Proveedor, Puestos, FechaInicio, FechaFin, DocumentoCompra, Costo, Observaciones);
}

/// <summary>Equipos a los que se asigna la licencia.</summary>
internal sealed record AsignarLicenciaRequest(IReadOnlyList<Guid> ActivoIds);
