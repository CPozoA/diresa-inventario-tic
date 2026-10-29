import { cierreDesdeFormulario, crearFormularioCierre } from './campos-cierre.component';

function formulario(proximoActual: string | null = null) {
  const grupo = crearFormularioCierre(() => '2026-10-01', { conservacion: 'Bueno', proximo: null, costo: 350 }, () => proximoActual);
  grupo.patchValue({ fechaFin: '2026-10-05', trabajo: '  Cambio de fuente  ', estadoActivoId: 'operativo' });
  return grupo;
}

describe('formulario de cierre', () => {
  it('el término no puede ser anterior al inicio', () => {
    const grupo = formulario();
    grupo.controls.fechaFin.setValue('2026-09-30');
    expect(grupo.controls.fechaFin.hasError('anterior')).toBe(true);
  });

  it('un próximo preventivo nuevo debe ser posterior al término', () => {
    const grupo = formulario();
    grupo.controls.proximo.setValue('2026-10-05');
    expect(grupo.controls.proximo.valid).toBe(false);
    grupo.controls.proximo.setValue('2026-10-06');
    expect(grupo.controls.proximo.valid).toBe(true);
  });

  it('se revalida el próximo al mover el término', () => {
    const grupo = formulario();
    grupo.controls.proximo.setValue('2026-10-06');
    grupo.controls.fechaFin.setValue('2026-10-07');
    expect(grupo.controls.proximo.valid).toBe(false);
  });

  it('puede quedar el próximo que ya tenía aunque esté vencido', () => {
    const grupo = formulario('2026-09-01');
    grupo.controls.proximo.setValue('2026-09-01');
    expect(grupo.controls.proximo.valid).toBe(true);
  });

  it('sin reparación no se valida ni se cambia el próximo', () => {
    const grupo = formulario('2026-12-01');
    grupo.controls.proximo.setValue('2026-01-01');
    grupo.controls.resultado.setValue('SinReparacion');
    expect(grupo.valid).toBe(true);
    expect(cierreDesdeFormulario(grupo, false, '2026-12-01').proximoMantenimiento).toBe('2026-12-01');
  });

  it('arma el cierre: el costo solo con proveedor y sin fecha queda sin programar', () => {
    const grupo = formulario();
    expect(cierreDesdeFormulario(grupo, true, null)).toEqual({
      fechaFin: '2026-10-05',
      trabajoRealizado: 'Cambio de fuente',
      resultado: 'Operativo',
      costo: 350,
      estadoActivoId: 'operativo',
      conservacion: 'Bueno',
      proximoMantenimiento: null,
    });
    expect(cierreDesdeFormulario(grupo, false, null).costo).toBeNull();
  });
});
