import { AsignacionActivoDto } from '../../../api/models/asignacion-activo-dto';
import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { conservacionAsignacion, detallePersona, estadoParaDevolucion, periodoAsignacion, ultimoMovimiento } from './asignacion';

const asignacion = (fechaEntrega: string, fechaDevolucion: string | null): AsignacionActivoDto => ({
  id: fechaEntrega,
  personaId: 'p1',
  dni: '41234567',
  nombreCompleto: 'JUAN PÉREZ QUISPE',
  cargo: null,
  establecimiento: 'Hospital de Huanta',
  area: 'Administración',
  verificada: true,
  fechaEntrega,
  conservacionEntrega: 'Nuevo',
  fechaDevolucion,
  conservacionDevolucion: fechaDevolucion ? 'Bueno' : null,
  observaciones: null,
});

const estado = (id: string, grupo: ElementoCatalogoDto['grupo'], activo = true): ElementoCatalogoDto =>
  ({ id, nombre: id, activo, grupo }) as ElementoCatalogoDto;

describe('asignacion', () => {
  it('arma el detalle de la persona sin huecos', () => {
    expect(detallePersona({ cargo: null, area: 'Farmacia', establecimiento: 'C.S. Huanta' })).toBe('Farmacia · C.S. Huanta');
    expect(detallePersona({ cargo: 'Digitadora', area: 'Estadística', establecimiento: 'Hospital' })).toBe('Digitadora · Estadística · Hospital');
  });

  it('propone el primer estado activo del grupo "en almacén"', () => {
    expect(estadoParaDevolucion([estado('op', 'EnUso'), estado('viejo', 'EnAlmacen', false), estado('alm', 'EnAlmacen')])).toBe('alm');
    expect(estadoParaDevolucion([estado('op', 'EnUso')])).toBeNull();
  });

  it('toma la fecha más reciente entre entregas y devoluciones', () => {
    expect(ultimoMovimiento([asignacion('2026-10-01', null), asignacion('2024-01-10', '2026-09-30')])).toBe('2026-10-01');
    expect(ultimoMovimiento([asignacion('2024-01-10', '2026-09-30')])).toBe('2026-09-30');
    expect(ultimoMovimiento([])).toBeNull();
  });

  it('describe el periodo y la conservación', () => {
    expect(periodoAsignacion(asignacion('2026-10-27', null))).toBe('Desde el 27/10/2026 · la tiene');
    expect(periodoAsignacion(asignacion('2023-03-15', '2026-10-26'))).toBe('15/03/2023 – 26/10/2026');
    expect(conservacionAsignacion(asignacion('2023-03-15', '2026-10-26'))).toBe('Nuevo → Bueno');
  });
});
