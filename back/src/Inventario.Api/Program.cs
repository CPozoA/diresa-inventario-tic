using Inventario.Api.Endpoints;
using Inventario.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Errores con formato estándar ProblemDetails (RFC 9457).
builder.Services.AddProblemDetails();

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

app.MapSaludEndpoints();

await app.RunAsync();
