import {
  conservacionHistorial,
  enMayusculas,
  esDniValido,
  estadoDesdeConsulta,
  filtrosPersonalDesdeParametros,
  hayFiltrosPersonal,
  iniciales,
  limpiarDni,
  parametrosDesdeFiltrosPersonal,
  seEscribenLosNombres,
  textoEquipos,
  textoMovimiento,
  tonoMovimiento,
} from './personal';

describe('personal', () => {
  it('toma las iniciales de las dos primeras palabras', () => {
    expect(iniciales('JUAN PÉREZ QUISPE')).toBe('JP');
    expect(iniciales('  ana  ')).toBe('A');
    expect(iniciales('')).toBe('');
  });

  it('cuenta los equipos en palabras', () => {
    expect(textoEquipos(0)).toBe('Ninguno');
    expect(textoEquipos('1')).toBe('1 equipo');
    expect(textoEquipos(3)).toBe('3 equipos');
  });

  it('nombra y colorea los movimientos', () => {
    expect(textoMovimiento('Devolucion')).toBe('Devolución');
    expect(tonoMovimiento('Entrega')).toBe('ok');
    expect(tonoMovimiento('Devolucion')).toBe('alerta');
  });

  it('muestra la conservación al entregar y, si la devolvió, también al devolver', () => {
    expect(conservacionHistorial('Bueno', null)).toBe('Bueno');
    expect(conservacionHistorial('Bueno', 'Regular')).toBe('Bueno → Regular');
  });

  it('deja solo los dígitos del DNI y exige 8', () => {
    expect(limpiarDni('41 234-567x9')).toBe('41234567');
    expect(esDniValido('41234567')).toBe(true);
    expect(esDniValido('4123456')).toBe(false);
  });

  it('pasa los nombres a mayúsculas, con tildes y eñes', () => {
    expect(enMayusculas('  maría   ñahui ')).toBe('MARÍA ÑAHUI');
  });

  it('decide qué hacer según la respuesta de la búsqueda del DNI', () => {
    const base = { dni: '41234567', apellidoPaterno: null, apellidoMaterno: null, nombres: null, registrada: null };
    expect(estadoDesdeConsulta({ ...base, reniec: 'Encontrado' })).toBe('encontrado');
    expect(estadoDesdeConsulta({ ...base, reniec: 'NoEncontrado' })).toBe('noEncontrado');
    expect(estadoDesdeConsulta({ ...base, reniec: 'NoDisponible' })).toBe('noDisponible');
    expect(estadoDesdeConsulta({ ...base, reniec: null, registrada: { id: 'p1', nombreCompleto: 'JUAN', activa: true } })).toBe(
      'registrada',
    );
    expect(seEscribenLosNombres('encontrado')).toBe(false);
    expect(seEscribenLosNombres('noDisponible')).toBe(true);
  });

  it('guarda los filtros en la dirección sin los valores por omisión', () => {
    const filtros = filtrosPersonalDesdeParametros({ vista: 'movimientos', buscar: ' quispe ', establecimiento: 'e1', cesados: 'si' });

    expect(filtros).toEqual({ vista: 'movimientos', buscar: 'quispe', establecimientoId: 'e1', cesados: true });
    expect(parametrosDesdeFiltrosPersonal(filtros)).toEqual({ vista: 'movimientos', buscar: 'quispe', establecimiento: 'e1', cesados: 'si' });
    expect(parametrosDesdeFiltrosPersonal(filtrosPersonalDesdeParametros({}))).toEqual({});
    expect(hayFiltrosPersonal(filtrosPersonalDesdeParametros({ vista: 'movimientos' }))).toBe(false);
  });
});
