using System.Reflection;
using Inventario.Application.Common.Mensajeria;

namespace Inventario.ArchitectureTests;

/// <summary>
/// Reglas de dependencia entre capas. Si alguien agrega una referencia que rompe la arquitectura
/// (por ejemplo, usar EF Core desde el dominio), estas pruebas fallan.
/// </summary>
public sealed class CapasTests
{
    private static readonly Assembly Domain = typeof(Inventario.Domain.Common.Entidad).Assembly;
    private static readonly Assembly Application = typeof(Inventario.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Inventario.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_NoDependeDeNingunaOtraCapaNiDeLibreriasExternas()
    {
        var referencias = NombresReferenciados(Domain);

        Assert.DoesNotContain(referencias, r => r.StartsWith("Inventario.", StringComparison.Ordinal));
        Assert.DoesNotContain(referencias, r => r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referencias, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referencias, r => r.StartsWith("Microsoft.Extensions", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_SoloDependeDelDominio()
    {
        var referencias = NombresReferenciados(Application);

        Assert.Contains("Inventario.Domain", referencias);
        Assert.DoesNotContain("Inventario.Infrastructure", referencias);
        Assert.DoesNotContain("Inventario.Api", referencias);
        Assert.DoesNotContain(referencias, r => r.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referencias, r => r.StartsWith("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_NoDependeDeLaApi() =>
        Assert.DoesNotContain("Inventario.Api", NombresReferenciados(Infrastructure));

    [Fact]
    public void Handlers_SonInternosYSellados()
    {
        var handlers = Application.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(EsInterfazDeHandler))
            .ToList();

        Assert.NotEmpty(handlers);
        Assert.All(handlers, h =>
        {
            Assert.False(h.IsPublic, $"{h.Name} debe ser internal: solo se usa a través de su interfaz.");
            Assert.True(h.IsSealed, $"{h.Name} debe ser sealed.");
        });
    }

    [Fact]
    public void Entidades_TienenConstructorSinParametrosPrivadoParaEfCore()
    {
        var entidades = Domain.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(Inventario.Domain.Common.Entidad)))
            .ToList();

        Assert.NotEmpty(entidades);
        Assert.All(entidades, e =>
        {
            var constructor = e.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes);
            Assert.True(constructor is { IsPrivate: true }, $"{e.Name} necesita un constructor privado sin parámetros.");
            Assert.Empty(e.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        });
    }

    private static bool EsInterfazDeHandler(Type interfaz) =>
        interfaz.IsGenericType
        && (interfaz.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
            || interfaz.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

    private static List<string> NombresReferenciados(Assembly ensamblado) =>
        ensamblado.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();
}
