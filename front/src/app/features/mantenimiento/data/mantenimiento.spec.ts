import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import {
  cuandoToca,
  diasEntre,
  estadoPropuesto,
  fechasMantenimiento,
  filtrosMantenimientoDesdeParametros,
  haceCuanto,
  parametrosDesdeFiltrosMantenimiento,
  proximoPropuesto,
  quien,
  sumarMeses,
  textoCosto,
} from './mantenimiento';

const estado = (id: string, grupo: ElementoCatalogoDto['grupo'], activo = true): ElementoCatalogoDto =>
  ({ id, nombre: id, activo, grupo }) as ElementoCatalogoDto;

describe('mantenimiento', () => {
  it('cuenta días sin zona horaria y los dice en palabras', () => {
    expect(diasEntre('2026-10-22', '2026-10-28')).toBe(6);
    expect(haceCuanto('2026-10-28', '2026-10-28')).toBe('hoy');
    expect(haceCuanto('2026-10-27', '2026-10-28')).toBe('ayer');
    expect(haceCuanto('2026-10-22', '2026-10-28')).toBe('hace 6 días');
  });

  it('dice cuándo toca el preventivo', () => {
    expect(cuandoToca('2026-10-10', '2026-10-28')).toEqual({ texto: 'Vencido hace 18 días', tono: 'mal' });
    expect(cuandoToca('2026-10-28', '2026-10-28').texto).toBe('Toca hoy');
    expect(cuandoToca('2026-11-02', '2026-10-28')).toEqual({ texto: 'En 5 días', tono: 'alerta' });
  });

  it('suma meses respetando el fin de mes', () => {
    expect(sumarMeses('2026-10-28', 6)).toBe('2027-04-28');
    expect(sumarMeses('2026-08-31', 6)).toBe('2027-02-28');
    expect(sumarMeses('2026-12-15', 1)).toBe('2027-01-15');
  });

  it('propone el próximo preventivo: 6 meses en un preventivo, el que tenía en un correctivo', () => {
    expect(proximoPropuesto('Preventivo', '2026-10-28', null)).toBe('2027-04-28');
    expect(proximoPropuesto('Correctivo', '2026-10-28', '2026-12-01')).toBe('2026-12-01');
    expect(proximoPropuesto('Correctivo', '2026-10-28', null)).toBeNull();
  });

  it('propone el estado según el resultado', () => {
    const estados = [estado('Inoperativo', 'FueraDeServicio'), estado('Operativo', 'EnUso'), estado('Prestado', 'EnUso'), estado('Malogrado', 'FueraDeServicio', false)];
    expect(estadoPropuesto(estados, 'Operativo')).toBe('Operativo');
    expect(estadoPropuesto(estados, 'SinReparacion')).toBe('Inoperativo');
    expect(estadoPropuesto([estado('Prestado', 'EnUso')], 'SinReparacion')).toBeNull();
  });

  it('muestra quién lo hizo, las fechas y el costo', () => {
    expect(quien({ ejecutor: 'Proveedor', tecnico: null, proveedor: 'Wari S.A.C.', ordenServicio: 'OS-1' })).toEqual({
      principal: 'Wari S.A.C.',
      detalle: 'Proveedor externo · OS-1',
    });
    expect(quien({ ejecutor: 'PersonalTi', tecnico: 'Jorge Quispe', proveedor: null, ordenServicio: null }).detalle).toBe('Personal de TI');
    expect(fechasMantenimiento({ fechaInicio: '2026-10-20', fechaFin: '2026-10-21' })).toBe('20/10/2026 – 21/10/2026');
    expect(fechasMantenimiento({ fechaInicio: '2026-10-20', fechaFin: '2026-10-20' })).toBe('20/10/2026');
    expect(textoCosto(null)).toBe('—');
  });

  it('guarda los filtros en la dirección sin los valores por omisión', () => {
    const f = filtrosMantenimientoDesdeParametros({ vista: 'historial', tipo: 'Preventivo', pagina: '3', buscar: ' pat ' });
    expect(f).toEqual({ vista: 'historial', buscar: 'pat', tipo: 'Preventivo', establecimientoId: null, pagina: 3 });
    expect(parametrosDesdeFiltrosMantenimiento(f)).toEqual({ vista: 'historial', tipo: 'Preventivo', pagina: '3', buscar: 'pat' });
    expect(parametrosDesdeFiltrosMantenimiento(filtrosMantenimientoDesdeParametros({ vista: 'otra', tipo: 'X' }))).toEqual({});
  });
});
