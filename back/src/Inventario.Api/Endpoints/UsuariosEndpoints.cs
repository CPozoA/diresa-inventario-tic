using Inventario.Api.Common;
using Inventario.Api.Seguridad;
using Inventario.Application.Common.Mensajeria;
using Inventario.Application.Common.Resultados;
using Inventario.Application.Usuarios;
using Inventario.Application.Usuarios.AsignarRoles;
using Inventario.Application.Usuarios.CambiarEstado;
using Inventario.Application.Usuarios.Crear;
using Inventario.Application.Usuarios.Desbloquear;
using Inventario.Application.Usuarios.Editar;
using Inventario.Application.Usuarios.Listar;
using Inventario.Application.Usuarios.Obtener;
using Inventario.Application.Usuarios.RestablecerContrasena;
using Inventario.Domain.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventario.Api.Endpoints;

/// <summary>Usuarios de sede. Las reglas contra el escalamiento de privilegios están en Application (ReglasDeGestion).</summary>
internal static class UsuariosEndpoints
{
    public static IEndpointRouteBuilder MapUsuariosEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/usuarios").WithTags("Usuarios");

        grupo.MapGet("/", ListarAsync)
            .RequirePermiso(Permisos.Usuarios.Ver)
            .WithName("ListarUsuarios")
            .WithSummary("Usuarios de la sede. El superadministrador puede filtrar con ?sedeId=. Búsqueda opcional con ?buscar=.");

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .RequirePermiso(Permisos.Usuarios.Ver)
            .WithName("ObtenerUsuario")
            .WithSummary("Un usuario de la sede.");

        grupo.MapPost("/", CrearAsync)
            .RequirePermiso(Permisos.Usuarios.Gestionar)
            .WithName("CrearUsuario")
            .WithSummary("Crea un usuario con contraseña temporal (se devuelve una sola vez).");

        grupo.MapPut("/{id:guid}", EditarAsync)
            .RequirePermiso(Permisos.Usuarios.Gestionar)
            .WithName("EditarUsuario")
            .WithSummary("Cambia nombres, apellidos, cargo y correo.");

        grupo.MapPut("/{id:guid}/roles", AsignarRolesAsync)
            .RequirePermiso(Permisos.Usuarios.Gestionar)
            .WithName("AsignarRolesUsuario")
            .WithSummary("Reemplaza los roles del usuario.");

        grupo.MapPost("/{id:guid}/activar", ActivarAsync)
            .RequirePermiso(Permisos.Usuarios.Gestionar)
            .WithName("ActivarUsuario")
            .WithSummary("Activa un usuario.");

        grupo.MapPost("/{id:guid}/desactivar", DesactivarAsync)
            .RequirePermiso(Permisos.Usuarios.Gestionar)
            .WithName("DesactivarUsuario")
            .WithSummary("Desactiva un usuario y cierra sus sesiones.");

        grupo.MapPost("/{id:guid}/restablecer-contrasena", RestablecerContrasenaAsync)
            .RequirePermiso(Permisos.Usuarios.Gestionar)
            .WithName("RestablecerContrasenaUsuario")
            .WithSummary("Genera una contraseña temporal (se devuelve una sola vez), quita el bloqueo y cierra sus sesiones.");

        grupo.MapPost("/{id:guid}/desbloquear", DesbloquearAsync)
            .RequirePermiso(Permisos.Usuarios.Gestionar)
            .WithName("DesbloquearUsuario")
            .WithSummary("Quita el bloqueo por intentos fallidos.");

        return app;
    }

    private static async Task<Results<Ok<IReadOnlyList<UsuarioDto>>, ProblemHttpResult>> ListarAsync(
        Guid? sedeId,
        string? buscar,
        IQueryHandler<ListarUsuariosQuery, IReadOnlyList<UsuarioDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ListarUsuariosQuery(sedeId, buscar), cancellationToken)).ToHttpResult();

    private static async Task<Results<Ok<UsuarioDto>, ProblemHttpResult>> ObtenerAsync(
        Guid id,
        IQueryHandler<ObtenerUsuarioQuery, UsuarioDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new ObtenerUsuarioQuery(id), cancellationToken)).ToHttpResult();

    private static async Task<Results<Created<UsuarioCreadoDto>, ProblemHttpResult>> CrearAsync(
        CrearUsuarioCommand comando,
        ICommandHandler<CrearUsuarioCommand, UsuarioCreadoDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(comando, cancellationToken)).ToCreatedResult(c => $"/api/usuarios/{c.Id}");

    private static async Task<Results<NoContent, ProblemHttpResult>> EditarAsync(
        Guid id,
        EditarUsuarioRequest cuerpo,
        ICommandHandler<EditarUsuarioCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(
            new EditarUsuarioCommand(id, cuerpo.Nombres, cuerpo.Apellidos, cuerpo.Cargo, cuerpo.Correo),
            cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> AsignarRolesAsync(
        Guid id,
        AsignarRolesRequest cuerpo,
        ICommandHandler<AsignarRolesUsuarioCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new AsignarRolesUsuarioCommand(id, cuerpo.RolIds), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> ActivarAsync(
        Guid id,
        ICommandHandler<CambiarEstadoUsuarioCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CambiarEstadoUsuarioCommand(id, Activo: true), cancellationToken)).ToNoContentResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DesactivarAsync(
        Guid id,
        ICommandHandler<CambiarEstadoUsuarioCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new CambiarEstadoUsuarioCommand(id, Activo: false), cancellationToken)).ToNoContentResult();

    private static async Task<Results<Ok<ContrasenaTemporalDto>, ProblemHttpResult>> RestablecerContrasenaAsync(
        Guid id,
        ICommandHandler<RestablecerContrasenaUsuarioCommand, ContrasenaTemporalDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new RestablecerContrasenaUsuarioCommand(id), cancellationToken)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DesbloquearAsync(
        Guid id,
        ICommandHandler<DesbloquearUsuarioCommand, Vacio> handler,
        CancellationToken cancellationToken) =>
        (await handler.Handle(new DesbloquearUsuarioCommand(id), cancellationToken)).ToNoContentResult();
}

internal sealed record EditarUsuarioRequest(string Nombres, string Apellidos, string? Cargo, string Correo);

internal sealed record AsignarRolesRequest(IReadOnlyList<Guid>? RolIds);
