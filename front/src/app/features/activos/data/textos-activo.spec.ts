import { aNumero, fechaRenovacion, formatoFecha, formatoGb, formatoSoles, tonoEstado } from './textos-activo';

describe('textos del activo', () => {
  it('reconoce el color de los estados por su nombre, sin tildes ni mayúsculas', () => {
    expect(tonoEstado('Operativo')).toBe('ok');
    expect(tonoEstado('En mantenimiento')).toBe('alerta');
    expect(tonoEstado('INOPERATIVO')).toBe('mal');
    expect(tonoEstado('De baja')).toBe('mal');
    expect(tonoEstado('En almacén')).toBe('neutra');
  });

  it('lee los enteros que la API envía como número o como texto', () => {
    expect(aNumero(16)).toBe(16);
    expect(aNumero('1536')).toBe(1536);
    expect(aNumero(null)).toBeNull();
    expect(aNumero('')).toBeNull();
    expect(aNumero('abc')).toBeNull();
  });

  it('da formato a capacidades y montos como en Perú', () => {
    expect(formatoGb(1536)).toBe('1,536 GB');
    expect(formatoGb(null)).toBe('—');
    expect(formatoSoles(3850)).toContain('3,850.00');
  });

  it('muestra las fechas sin correrlas por la zona horaria', () => {
    expect(formatoFecha('2023-03-15')).toBe('15/03/2023');
    expect(formatoFecha(null)).toBe('—');
    expect(formatoFecha('no es fecha')).toBe('—');
  });

  it('calcula la renovación prevista como la API (fecha de adquisición más la vida útil)', () => {
    expect(fechaRenovacion('2023-03-15', 4)).toBe('2027-03-15');
    expect(fechaRenovacion('2024-02-29', 1)).toBe('2025-02-28');
    expect(fechaRenovacion('2024-02-29', 4)).toBe('2028-02-29');
    expect(fechaRenovacion(null, 4)).toBeNull();
    expect(fechaRenovacion('2023-03-15', null)).toBeNull();
  });
});
