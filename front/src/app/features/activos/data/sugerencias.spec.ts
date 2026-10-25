import { filtrosConValor } from './sugerencias';

describe('sugerencias de campos escritos', () => {
  it('no envía los filtros vacíos', () => {
    expect(filtrosConValor({ marcaId: 'm1', sistemaOperativoId: undefined, establecimientoId: '' })).toEqual({ marcaId: 'm1' });
    expect(filtrosConValor({})).toEqual({});
  });
});
