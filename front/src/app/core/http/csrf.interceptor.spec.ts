import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { provideApiConfiguration } from '../../api/api-configuration';
import { CABECERA_CSRF, CODIGO_CSRF_INVALIDO, csrfInterceptor } from './csrf.interceptor';

/** Deja correr las promesas pendientes (el interceptor espera el token de forma asíncrona). */
const esperar = () => new Promise<void>((resolver) => setTimeout(resolver));

describe('csrfInterceptor', () => {
  let http: HttpClient;
  let servidor: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([csrfInterceptor])),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
      ],
    });
    http = TestBed.inject(HttpClient);
    servidor = TestBed.inject(HttpTestingController);
  });

  afterEach(() => servidor.verify());

  it('no toca las consultas (GET)', async () => {
    const respuesta = firstValueFrom(http.get('/api/sedes'));

    const peticion = servidor.expectOne('/api/sedes');
    expect(peticion.request.headers.has(CABECERA_CSRF)).toBe(false);
    peticion.flush([]);

    await respuesta;
  });

  it('pide el token una vez y lo envía en cada cambio', async () => {
    const primera = firstValueFrom(http.post('/api/roles', {}));
    servidor.expectOne('/api/cuenta/csrf').flush({ token: 'token-1' });
    await esperar();
    const post1 = servidor.expectOne('/api/roles');
    expect(post1.request.headers.get(CABECERA_CSRF)).toBe('token-1');
    post1.flush({ id: 'x' });
    await primera;

    const segunda = firstValueFrom(http.delete('/api/roles/x'));
    await esperar();
    const del = servidor.expectOne('/api/roles/x');
    expect(del.request.headers.get(CABECERA_CSRF)).toBe('token-1');
    del.flush(null, { status: 204, statusText: 'No Content' });
    await segunda;
  });

  it('si la API rechaza el token, pide otro y reintenta una sola vez', async () => {
    const respuesta = firstValueFrom(http.post('/api/cuenta/cerrar-sesion', null));
    servidor.expectOne('/api/cuenta/csrf').flush({ token: 'viejo' });
    await esperar();

    servidor
      .expectOne('/api/cuenta/cerrar-sesion')
      .flush({ title: 'No verificada', codigo: CODIGO_CSRF_INVALIDO }, { status: 400, statusText: 'Bad Request' });
    await esperar();

    servidor.expectOne('/api/cuenta/csrf').flush({ token: 'nuevo' });
    await esperar();

    const reintento = servidor.expectOne('/api/cuenta/cerrar-sesion');
    expect(reintento.request.headers.get(CABECERA_CSRF)).toBe('nuevo');
    reintento.flush(null, { status: 204, statusText: 'No Content' });

    await respuesta;
  });
});
