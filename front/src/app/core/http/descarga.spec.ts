import { nombreDesdeEncabezado } from './descarga';

describe('descarga', () => {
  it('lee el nombre del archivo de Content-Disposition', () => {
    expect(
      nombreDesdeEncabezado(
        "attachment; filename=Inventario-Red-de-Salud-Huanta-2026-10-30.xlsx; filename*=UTF-8''Inventario-Red-de-Salud-Huanta-2026-10-30.xlsx",
        'x.xlsx',
      ),
    ).toBe('Inventario-Red-de-Salud-Huanta-2026-10-30.xlsx');
    expect(nombreDesdeEncabezado("attachment; filename*=UTF-8''Informaci%C3%B3n.xlsx", 'x.xlsx')).toBe('Información.xlsx');
    expect(nombreDesdeEncabezado('attachment; filename="Inventario.xlsx"', 'x.xlsx')).toBe('Inventario.xlsx');
    expect(nombreDesdeEncabezado(null, 'x.xlsx')).toBe('x.xlsx');
    expect(nombreDesdeEncabezado('attachment', 'x.xlsx')).toBe('x.xlsx');
  });
});
