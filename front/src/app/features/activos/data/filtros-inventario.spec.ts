import { consultaDesdeFiltros } from './inventario.store';
import {
  FILTROS_INICIALES,
  cambiarFiltro,
  filtrosDesdeParametros,
  hayFiltros,
  parametrosDesdeFiltros,
  sinFiltros,
} from './filtros-inventario';

describe('filtros del inventario', () => {
  it('sin parámetros en la dirección quedan los filtros iniciales', () => {
    expect(filtrosDesdeParametros({})).toEqual(FILTROS_INICIALES);
    expect(hayFiltros(FILTROS_INICIALES)).toBe(false);
  });

  it('lee los filtros de la dirección e ignora lo que no reconoce', () => {
    const filtros = filtrosDesdeParametros({
      buscar: ' dell ',
      tipo: 't1',
      garantia: 'Vencida',
      renovacion: 'Inventada',
      pagina: '-3',
      filas: '37',
    });

    expect(filtros.buscar).toBe('dell');
    expect(filtros.tipoActivoId).toBe('t1');
    expect(filtros.garantia).toBe('Vencida');
    expect(filtros.renovacion).toBeNull();
    expect(filtros.pagina).toBe(1);
    expect(filtros.tamanoPagina).toBe(25);
  });

  it('escribe en la dirección solo lo que cambió y se puede volver a leer igual', () => {
    const filtros = { ...FILTROS_INICIALES, buscar: 'Latitude', estadoActivoId: 'e1', renovacion: 'Renovar' as const, pagina: 3, tamanoPagina: 50 };
    const parametros = parametrosDesdeFiltros(filtros);

    expect(parametros).toEqual({ buscar: 'Latitude', estado: 'e1', renovacion: 'Renovar', pagina: '3', filas: '50' });
    expect(filtrosDesdeParametros(parametros)).toEqual(filtros);
    expect(parametrosDesdeFiltros(FILTROS_INICIALES)).toEqual({});
  });

  it('al cambiar un filtro se vuelve a la primera página', () => {
    const enPagina4 = { ...FILTROS_INICIALES, pagina: 4 };
    const filtrado = cambiarFiltro(enPagina4, 'garantia', 'Vigente');

    expect(filtrado.garantia).toBe('Vigente');
    expect(filtrado.pagina).toBe(1);
    expect(hayFiltros(filtrado)).toBe(true);
  });

  it('quitar filtros conserva las filas por página', () => {
    const filtros = { ...FILTROS_INICIALES, buscar: 'hp', tipoActivoId: 't1', pagina: 2, tamanoPagina: 100 };

    expect(sinFiltros(filtros)).toEqual({ ...FILTROS_INICIALES, tamanoPagina: 100 });
  });

  it('a la API solo se envían los filtros con valor', () => {
    const filtros = { ...FILTROS_INICIALES, buscar: '  pc-01 ', establecimientoId: 'est1' };

    expect(consultaDesdeFiltros(filtros, null)).toEqual({ pagina: 1, tamanoPagina: 25, buscar: 'pc-01', establecimientoId: 'est1' });
    expect(consultaDesdeFiltros(FILTROS_INICIALES, 's1')).toEqual({ pagina: 1, tamanoPagina: 25, sedeId: 's1' });
  });
});
