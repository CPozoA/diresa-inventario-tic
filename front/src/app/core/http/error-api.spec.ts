import { HttpErrorResponse } from '@angular/common/http';

import { aErrorApi } from './error-api';

describe('aErrorApi', () => {
  it('usa el mensaje y el código de un ProblemDetails de la API', () => {
    const error = new HttpErrorResponse({
      status: 422,
      error: { title: 'Su cuenta está bloqueada.', status: 422, codigo: 'Cuenta.Bloqueada' },
    });

    expect(aErrorApi(error)).toEqual({ estado: 422, codigo: 'Cuenta.Bloqueada', mensaje: 'Su cuenta está bloqueada.' });
  });

  it('no muestra detalles internos de un error 500', () => {
    const error = new HttpErrorResponse({ status: 500, error: { title: 'NullReferenceException en ...' } });

    const resultado = aErrorApi(error);

    expect(resultado.estado).toBe(500);
    expect(resultado.mensaje).not.toContain('NullReference');
  });

  it('reconoce la falta de conexión', () => {
    expect(aErrorApi(new HttpErrorResponse({ status: 0 })).estado).toBe(0);
  });

  it('traduce un 403 sin código (falta de permiso)', () => {
    const resultado = aErrorApi(new HttpErrorResponse({ status: 403, error: { title: 'Forbidden', status: 403 } }));

    expect(resultado.codigo).toBeNull();
    expect(resultado.mensaje).toBe('No tiene permiso para realizar esta acción.');
  });
});
