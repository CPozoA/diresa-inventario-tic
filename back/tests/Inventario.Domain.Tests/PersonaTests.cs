using Inventario.Domain.Common;
using Inventario.Domain.Personal;

namespace Inventario.Domain.Tests;

public sealed class PersonaTests
{
    private static readonly Guid Sede = Guid.CreateVersion7();
    private static readonly DateTimeOffset Ahora = new(2026, 10, 28, 15, 0, 0, TimeSpan.Zero);

    private static DatosLaboralesPersona Laborales(string? cargo = "  Asistente   administrativo ") =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), cargo);

    [Fact]
    public void RegistrarVerificada_GuardaLosNombresEnMayusculasYArmaElNombreCompleto()
    {
        var persona = Persona.RegistrarVerificada(
            Sede, " 41234567 ", new NombresPersona("pérez", "quispe", "  juan   carlos "), Laborales(), Ahora);

        Assert.Equal("41234567", persona.Dni);
        Assert.Equal("PÉREZ", persona.ApellidoPaterno);
        Assert.Equal("QUISPE", persona.ApellidoMaterno);
        Assert.Equal("JUAN CARLOS", persona.Nombres);
        Assert.Equal("JUAN CARLOS PÉREZ QUISPE", persona.NombreCompleto);
        Assert.Equal("Asistente administrativo", persona.Cargo);
        Assert.True(persona.Verificada);
        Assert.Equal(Ahora, persona.VerificadaEn);
        Assert.True(persona.Activa);
    }

    [Fact]
    public void RegistrarSinVerificar_AceptaSinApellidoMaterno_YQuedaSinVerificar()
    {
        var persona = Persona.RegistrarSinVerificar(Sede, "47012345", new NombresPersona("Ñahui", " ", "Ana María"), Laborales(null));

        Assert.False(persona.Verificada);
        Assert.Null(persona.ApellidoMaterno);
        Assert.Null(persona.Cargo);
        Assert.Equal("ANA MARÍA ÑAHUI", persona.NombreCompleto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("4123456A")]
    public void ElDniDebeTener8Digitos(string dni) =>
        Assert.Throws<DominioException>(() => Persona.RegistrarSinVerificar(Sede, dni, new NombresPersona("Pérez", null, "Juan"), Laborales()));

    [Fact]
    public void LosNombresSoloLlevanLetras_YElApellidoPaternoYLosNombresSonObligatorios()
    {
        Assert.Throws<DominioException>(() => Persona.RegistrarSinVerificar(Sede, "41234567", new NombresPersona("Pérez 2", null, "Juan"), Laborales()));
        Assert.Throws<DominioException>(() => Persona.RegistrarSinVerificar(Sede, "41234567", new NombresPersona(" ", null, "Juan"), Laborales()));
        Assert.Throws<DominioException>(() => Persona.RegistrarSinVerificar(Sede, "41234567", new NombresPersona("Pérez", null, ""), Laborales()));

        var conApostrofo = Persona.RegistrarSinVerificar(Sede, "41234567", new NombresPersona("D'Angelo", "Mendoza-Ríos", "José"), Laborales());
        Assert.Equal("JOSÉ D'ANGELO MENDOZA-RÍOS", conApostrofo.NombreCompleto);
    }

    [Fact]
    public void ElEstablecimientoYElAreaSonObligatorios()
    {
        var nombres = new NombresPersona("Pérez", null, "Juan");
        Assert.Throws<DominioException>(() => Persona.RegistrarSinVerificar(Sede, "41234567", nombres, new DatosLaboralesPersona(Guid.Empty, Guid.CreateVersion7(), null)));
        Assert.Throws<DominioException>(() => Persona.RegistrarSinVerificar(Sede, "41234567", nombres, new DatosLaboralesPersona(Guid.CreateVersion7(), Guid.Empty, null)));
    }

    [Fact]
    public void LosNombresDeUnaPersonaVerificadaNoSeCorrigenAMano()
    {
        var persona = Persona.RegistrarVerificada(Sede, "41234567", new NombresPersona("Pérez", "Quispe", "Juan"), Laborales(), Ahora);

        Assert.Throws<DominioException>(() => persona.CorregirNombres(new NombresPersona("Otro", null, "Nombre")));
    }

    [Fact]
    public void Verificar_ReemplazaLosNombresEscritosAMano()
    {
        var persona = Persona.RegistrarSinVerificar(Sede, "46543210", new NombresPersona("Taype", null, "Maria"), Laborales());
        persona.CorregirNombres(new NombresPersona("Taipe", null, "María"));
        Assert.Equal("MARÍA TAIPE", persona.NombreCompleto);

        persona.Verificar(new NombresPersona("TAIPE", "HUAMÁN", "MARÍA"), Ahora);

        Assert.True(persona.Verificada);
        Assert.Equal("MARÍA TAIPE HUAMÁN", persona.NombreCompleto);
    }

    [Fact]
    public void CesarYReactivar()
    {
        var persona = Persona.RegistrarSinVerificar(Sede, "41234567", new NombresPersona("Pérez", null, "Juan"), Laborales());

        persona.Cesar();
        Assert.False(persona.Activa);

        persona.Reactivar();
        Assert.True(persona.Activa);
    }
}
