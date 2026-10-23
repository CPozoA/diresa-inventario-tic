import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';

import { provideApiConfiguration } from '../../api/api-configuration';
import { SesionDto } from '../../api/models/sesion-dto';
import { autenticadoGuard, permisoGuard } from './guards';
import { Permisos } from './permisos';

const sesionEditor = (debeCambiar: boolean): SesionDto => ({
  usuarioId: 'u1',
  nombreUsuario: 'editor.huanta',
  nombreCompleto: 'Editor Huanta',
  correo: null,
  sede: { id: 's1', codigo: 'HUANTA', nombre: 'Red de Salud Huanta' },
  esSuperAdmin: false,
  debeCambiarContrasena: debeCambiar,
  permisos: [Permisos.Activos.Ver],
});

describe('guards de sesión', () => {
  let servidor: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration('')],
    });
    servidor = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => servidor.verify());

  function ejecutar(guard: typeof autenticadoGuard, url: string): Promise<unknown> {
    const resultado = TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );
    return Promise.resolve(resultado);
  }

  it('sin sesión lleva al inicio de sesión recordando a dónde quería ir', async () => {
    const resultado = ejecutar(autenticadoGuard, '/usuarios');
    servidor.expectOne('/api/cuenta/sesion').flush(null, { status: 401, statusText: 'Unauthorized' });

    const destino = (await resultado) as UrlTree;
    expect(router.serializeUrl(destino)).toBe('/iniciar-sesion?volver=%2Fusuarios');
  });

  it('con contraseña temporal solo deja ir a cambiarla', async () => {
    const resultado = ejecutar(autenticadoGuard, '/');
    servidor.expectOne('/api/cuenta/sesion').flush(sesionEditor(true));

    expect(router.serializeUrl((await resultado) as UrlTree)).toBe('/cambiar-contrasena');
    expect(await ejecutar(autenticadoGuard, '/cambiar-contrasena')).toBe(true);
  });

  it('sin el permiso de la pantalla, vuelve al inicio', async () => {
    const resultado = ejecutar(permisoGuard(Permisos.Usuarios.Ver), '/usuarios');
    servidor.expectOne('/api/cuenta/sesion').flush(sesionEditor(false));

    expect(router.serializeUrl((await resultado) as UrlTree)).toBe('/');
    expect(await ejecutar(permisoGuard(Permisos.Activos.Ver), '/inventario')).toBe(true);
  });
});
