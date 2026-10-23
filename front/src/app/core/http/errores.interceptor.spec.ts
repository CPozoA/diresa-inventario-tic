import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { SesionStore } from '../auth/sesion.store';
import { SIN_REDIRECCION_401 } from './contexto-http';
import { CODIGO_DEBE_CAMBIAR_CONTRASENA, erroresInterceptor } from './errores.interceptor';

describe('erroresInterceptor', () => {
  let http: HttpClient;
  let servidor: HttpTestingController;
  let navegar: ReturnType<typeof vi.spyOn>;
  const sesionFalsa = { autenticado: vi.fn(() => true), limpiar: vi.fn() };

  beforeEach(() => {
    sesionFalsa.autenticado.mockReturnValue(true);
    sesionFalsa.limpiar.mockClear();

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([erroresInterceptor])),
        provideHttpClientTesting(),
        { provide: SesionStore, useValue: sesionFalsa },
      ],
    });
    http = TestBed.inject(HttpClient);
    servidor = TestBed.inject(HttpTestingController);
    navegar = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => servidor.verify());

  it('con 401 olvida la sesión, avisa que venció y propaga el error', async () => {
    const respuesta = firstValueFrom(http.get('/api/usuarios'));
    servidor.expectOne('/api/usuarios').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(respuesta).rejects.toMatchObject({ status: 401 });
    expect(sesionFalsa.limpiar).toHaveBeenCalled();
    expect(navegar).toHaveBeenCalledWith(['/iniciar-sesion'], { queryParams: { motivo: 'sesion-vencida' } });
  });

  it('no redirige si la petición lo pide (consulta inicial de la sesión)', async () => {
    const contexto = new HttpContext().set(SIN_REDIRECCION_401, true);
    const respuesta = firstValueFrom(http.get('/api/cuenta/sesion', { context: contexto }));
    servidor.expectOne('/api/cuenta/sesion').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(respuesta).rejects.toMatchObject({ status: 401 });
    expect(navegar).not.toHaveBeenCalled();
    expect(sesionFalsa.limpiar).not.toHaveBeenCalled();
  });

  it('con contraseña temporal lleva a cambiarla', async () => {
    const respuesta = firstValueFrom(http.get('/api/sedes'));
    servidor
      .expectOne('/api/sedes')
      .flush({ title: 'Debe cambiar…', codigo: CODIGO_DEBE_CAMBIAR_CONTRASENA }, { status: 403, statusText: 'Forbidden' });

    await expect(respuesta).rejects.toMatchObject({ status: 403 });
    expect(navegar).toHaveBeenCalledWith(['/cambiar-contrasena']);
  });
});
