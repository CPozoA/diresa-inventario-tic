import { FormControl, FormGroup } from '@angular/forms';

import { decimales, entero, esIpValida, esMacValida, noAnteriorA, noPosteriorA } from './validadores-activo';

describe('validaciones del formulario de activos', () => {
  it('reconoce direcciones IP', () => {
    expect(esIpValida('192.168.10.45')).toBe(true);
    expect(esIpValida('10.0.0.1')).toBe(true);
    expect(esIpValida('fe80::1')).toBe(true);
    expect(esIpValida('192.168.10')).toBe(false);
    expect(esIpValida('192.168.10.256')).toBe(false);
    expect(esIpValida('192.168.010.45')).toBe(false);
    expect(esIpValida('servidor')).toBe(false);
  });

  it('acepta la MAC con los separadores habituales', () => {
    expect(esMacValida('A4:BB:6D:12:34:56')).toBe(true);
    expect(esMacValida('a4-bb-6d-12-34-56')).toBe(true);
    expect(esMacValida('a4bb.6d12.3456')).toBe(true);
    expect(esMacValida('A4BB6D123456')).toBe(true);
    expect(esMacValida('A4:BB:6D:12:34')).toBe(false);
    expect(esMacValida('G4:BB:6D:12:34:56')).toBe(false);
  });

  it('la fecha de adquisición no puede ser futura', () => {
    const validar = noPosteriorA(() => '2026-10-25');
    expect(validar(new FormControl('2026-10-25'))).toBeNull();
    expect(validar(new FormControl('2026-10-26'))).toEqual({ futura: true });
    expect(validar(new FormControl(''))).toBeNull();
  });

  it('el fin de garantía no puede ser anterior a la adquisición', () => {
    const grupo = new FormGroup({
      fechaAdquisicion: new FormControl('2023-03-15'),
      finGarantia: new FormControl('2023-03-14', noAnteriorA('fechaAdquisicion')),
    });
    // Al crear el control todavía no tiene grupo: se valida otra vez ya dentro de él (como al cargar el formulario).
    grupo.controls.finGarantia.updateValueAndValidity();
    expect(grupo.controls.finGarantia.errors).toEqual({ anterior: true });

    grupo.controls.finGarantia.setValue('2024-03-14');
    expect(grupo.controls.finGarantia.errors).toBeNull();
  });

  it('controla enteros y decimales', () => {
    expect(entero(new FormControl(4))).toBeNull();
    expect(entero(new FormControl(4.5))).toEqual({ entero: true });
    expect(decimales(2)(new FormControl(3850.5))).toBeNull();
    expect(decimales(2)(new FormControl(3850.555))).toEqual({ decimales: { maximo: 2 } });
  });
});
