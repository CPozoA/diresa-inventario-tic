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

// Contrato OpenAPI: lo usan Scalar (documentación) y, más adelante, el generador del cliente de Angular.
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttp>();

builder.Services.AddSeguridadHttp();
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
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<ExigirCambioContrasenaMiddleware>();

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

await app.Services.InicializarBaseDatosAsync();

await app.RunAsync();
