import {
  filtrosLicenciasDesdeParametros,
  parametrosDesdeFiltrosLicencias,
  periodoRenovacion,
  porcentajeUso,
  sumarDias,
  textoLibres,
  textoPuestos,
  textoVigencia,
  tonoVigencia,
  unAnoDesde,
} from './licencias';

describe('licencias', () => {
  it('dice la vigencia en palabras', () => {
    expect(textoVigencia('Vigente', 200)).toBe('Vigente');
    expect(textoVigencia('PorVencer', 12)).toBe('Vence en 12 días');
    expect(textoVigencia('PorVencer', 0)).toBe('Vence hoy');
    expect(textoVigencia('PorVencer', '1')).toBe('Vence mañana');
    expect(textoVigencia('Vencida', -1)).toBe('Venció ayer');
    expect(textoVigencia('Vencida', -40)).toBe('Venció hace 40 días');
    expect(textoVigencia('Archivada', -400)).toBe('Archivada');
    expect(tonoVigencia('PorVencer')).toBe('alerta');
    expect(tonoVigencia('Vencida')).toBe('mal');
  });

  it('cuenta puestos libres y los de más', () => {
    expect(textoPuestos(3, 5)).toBe('3 de 5 puestos');
    expect(textoPuestos(0, '1')).toBe('0 de 1 puesto');
    expect(textoLibres(3, 5)).toBe('2 libres');
    expect(textoLibres(4, 5)).toBe('1 libre');
    expect(textoLibres(5, 5)).toBe('Sin puestos libres');
    expect(textoLibres(7, 5)).toBe('2 equipos de más');
    expect(porcentajeUso(3, 4)).toBe(75);
    expect(porcentajeUso(9, 4)).toBe(100);
  });

  it('propone el periodo de renovación: un año desde el día siguiente', () => {
    expect(sumarDias('2026-12-31', 1)).toBe('2027-01-01');
    expect(unAnoDesde('2026-03-01')).toBe('2027-02-28');
    expect(periodoRenovacion('2027-02-28')).toEqual({ inicio: '2027-03-01', fin: '2028-02-29' });
  });

  it('guarda los filtros en la dirección y descarta los desconocidos', () => {
    expect(filtrosLicenciasDesdeParametros({ buscar: ' office ', filtro: 'Vencidas' })).toEqual({ buscar: 'office', filtro: 'Vencidas' });
    expect(filtrosLicenciasDesdeParametros({ filtro: 'Otra' }).filtro).toBeNull();
    expect(parametrosDesdeFiltrosLicencias({ buscar: '', filtro: 'Excedidas' })).toEqual({ filtro: 'Excedidas' });
  });
});
