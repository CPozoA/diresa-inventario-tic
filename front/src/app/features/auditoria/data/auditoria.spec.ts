import { esAlerta, fechaHora, filtrosAuditoriaDesdeParametros, parametrosDesdeFiltrosAuditoria, textoModulo } from './auditoria';

describe('auditoria', () => {
  it('nombra los módulos y resalta los intentos fallidos', () => {
    expect(textoModulo('Catalogos')).toBe('Catálogos');
    expect(textoModulo('Cuenta')).toBe('Cuenta y sesiones');
    expect(esAlerta('Inicio de sesión fallido: contraseña incorrecta')).toBe(true);
    expect(esAlerta('Intento de inicio de sesión con la cuenta bloqueada (3 intentos fallidos seguidos)')).toBe(true);
    expect(esAlerta('Inició sesión')).toBe(false);
  });

  it('muestra la fecha y hora local', () => {
    const local = new Date(2026, 9, 30, 9, 5);
    expect(fechaHora(local.toISOString())).toBe('30/10/2026 09:05');
    expect(fechaHora('no es fecha')).toBe('—');
  });

  it('guarda los filtros en la dirección y descarta los inválidos', () => {
    const f = filtrosAuditoriaDesdeParametros({ desde: '2026-10-01', hasta: 'ayer', modulo: 'Licencias', buscar: ' PAT ', pagina: '3' });
    expect(f).toEqual({ desde: '2026-10-01', hasta: '', modulo: 'Licencias', buscar: 'PAT', pagina: 3 });
    expect(filtrosAuditoriaDesdeParametros({ modulo: 'Otro' }).modulo).toBeNull();
    expect(parametrosDesdeFiltrosAuditoria({ ...f, pagina: 1 })).toEqual({ desde: '2026-10-01', modulo: 'Licencias', buscar: 'PAT' });
  });
});
