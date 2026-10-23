import { cumpleTodas, evaluarContrasena } from './reglas-contrasena';

describe('evaluarContrasena', () => {
  it('acepta una contraseña que cumple la política', () => {
    expect(cumpleTodas(evaluarContrasena('Huanta-2026x', 'Huanta-2026x'))).toBe(true);
  });

  it('marca exactamente lo que falta', () => {
    const reglas = evaluarContrasena('corta1', 'corta1');
    const faltan = reglas.filter((r) => !r.cumple).map((r) => r.texto);

    expect(faltan).toEqual(['Al menos 10 caracteres', 'Una letra mayúscula']);
  });

  it('cuenta las letras con tilde y la ñ como mayúsculas o minúsculas', () => {
    const reglas = evaluarContrasena('ÑANDÚ-12345', 'ÑANDÚ-12345');

    expect(reglas.find((r) => r.texto === 'Una letra mayúscula')?.cumple).toBe(true);
    expect(reglas.find((r) => r.texto === 'Una letra minúscula')?.cumple).toBe(false);
  });

  it('con la contraseña actual, exige que la nueva sea distinta', () => {
    const reglas = evaluarContrasena('Huanta-2026x', 'Huanta-2026x', 'Huanta-2026x');

    expect(reglas.find((r) => r.texto === 'Distinta de la actual')?.cumple).toBe(false);
  });

  it('exige que la confirmación coincida', () => {
    expect(cumpleTodas(evaluarContrasena('Huanta-2026x', 'Huanta-2026y'))).toBe(false);
  });
});
