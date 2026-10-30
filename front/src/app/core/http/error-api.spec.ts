import { HttpErrorResponse } from '@angular/common/http';

import { aErrorApi, aErrorApiDeDescarga } from './error-api';

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

  it('un 503 con código (servicio externo no disponible) trae su mensaje; sin código, el genérico', () => {
    const conCodigo = new HttpErrorResponse({
      status: 503,
      error: { title: 'No se pudo consultar RENIEC en este momento.', codigo: 'Personal.ReniecNoDisponible' },
    });
    const sinCodigo = new HttpErrorResponse({ status: 503, error: { title: 'Service Unavailable' } });

    expect(aErrorApi(conCodigo).mensaje).toBe('No se pudo consultar RENIEC en este momento.');
    expect(aErrorApi(sinCodigo).mensaje).not.toContain('Service');
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

describe('aErrorApiDeDescarga', () => {
  it('lee el ProblemDetails que llega como Blob al pedir un archivo', async () => {
    const cuerpo = new Blob([JSON.stringify({ title: 'Son 25 000 activos.', codigo: 'Exportacion.DemasiadosActivos' })], {
      type: 'application/problem+json',
    });
    const error = new HttpErrorResponse({ status: 422, error: cuerpo });

    expect(await aErrorApiDeDescarga(error)).toEqual({ estado: 422, codigo: 'Exportacion.DemasiadosActivos', mensaje: 'Son 25 000 activos.' });
  });
});
