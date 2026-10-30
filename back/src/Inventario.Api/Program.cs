using System.Text.Json.Serialization;
using Inventario.Api.Common;
using Inventario.Api.Endpoints;
using Inventario.Api.Seguridad;
using Inventario.Application;
using Inventario.Application.Common.Seguridad;
using Inventario.Infrastructure;
using Inventario.Infrastructure.Persistencia.Inicializacion;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Errores con formato estándar ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();

// Una regla del dominio incumplida (dato inválido) responde 400 con su mensaje; lo demás, 500 genérico.
builder.Services.AddExceptionHandler<ManejadorExcepcionDominio>();

// Las listas fijas (conservación, tipo de disco…) viajan como texto ("Bueno", "Nvme"), no como números.
builder.Services.ConfigureHttpJsonOptions(opciones => opciones.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Contrato OpenAPI: lo usan Scalar (documentación) y, más adelante, el generador del cliente de Angular.
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttp>();

builder.Services.AddSeguridadHttp(builder.Configuration);
builder.Services.AddProteccionCsrf();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Errores inesperados: respuesta genérica, sin detalles internos.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();

// Después de identificar al usuario (el token está ligado a él): rechaza cambios sin token antifalsificación válido.
app.UseMiddleware<ValidarCsrfMiddleware>();

// Después de identificar al usuario y ANTES de autorizar: con una contraseña temporal la respuesta debe ser
// "cambie su contraseña" (Cuenta.DebeCambiarContrasena), no "no tiene permiso".
app.UseMiddleware<ExigirCambioContrasenaMiddleware>();

app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    // Documentación interactiva solo en desarrollo: /openapi/v1.json y /scalar.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// Toda la API exige sesión iniciada, salvo los endpoints marcados con AllowAnonymous()
// (salud, iniciar sesión y recuperación de contraseña).
var api = app.MapGroup("/api").RequireAuthorization();

api.MapSaludEndpoints();
api.MapCuentaEndpoints();
api.MapSedesEndpoints();
api.MapRolesEndpoints();
api.MapUsuariosEndpoints();
api.MapCatalogosEndpoints();
api.MapEstablecimientosEndpoints();
api.MapActivosEndpoints();
api.MapFotosActivoEndpoints();
api.MapPersonalEndpoints();
api.MapAsignacionesEndpoints();
api.MapMantenimientosEndpoints();
api.MapLicenciasEndpoints();
api.MapResumenEndpoints();

await app.Services.InicializarBaseDatosAsync();

await app.RunAsync();
