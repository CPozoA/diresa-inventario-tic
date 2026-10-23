import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { provideApiConfiguration } from '../../api/api-configuration';
import { SesionDto } from '../../api/models/sesion-dto';
import { Permisos } from './permisos';
import { SesionStore } from './sesion.store';

const sesionAdmin: SesionDto = {
  usuarioId: '019a0b6e-0000-7000-8000-000000000001',
  nombreUsuario: 'admin.huanta',
  nombreCompleto: 'Admin Huanta',
  correo: 'admin@huanta.test',
  sede: { id: '019a0b6e-0000-7000-8000-000000000002', codigo: 'HUANTA', nombre: 'Red de Salud Huanta' },
  esSuperAdmin: false,
  debeCambiarContrasena: false,
  permisos: [Permisos.Usuarios.Ver, Permisos.Usuarios.Gestionar],
};

describe('SesionStore', () => {
  let store: InstanceType<typeof SesionStore>;
  let servidor: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration('')],
    });
    store = TestBed.inject(SesionStore);
    servidor = TestBed.inject(HttpTestingController);
  });

  afterEach(() => servidor.verify());

  it('un 401 al verificar significa "sin sesión", no un error', async () => {
    const verificacion = store.asegurarVerificada();
    servidor.expectOne('/api/cuenta/sesion').flush(null, { status: 401, statusText: 'Unauthorized' });
    await verificacion;

    expect(store.carga()).toBe('verificada');
    expect(store.autenticado()).toBe(false);
  });

  it('con sesión expone los permisos', async () => {
    const verificacion = store.asegurarVerificada();
    servidor.expectOne('/api/cuenta/sesion').flush(sesionAdmin);
    await verificacion;

    expect(store.autenticado()).toBe(true);
    expect(store.tienePermiso(Permisos.Usuarios.Gestionar)).toBe(true);
    expect(store.tienePermiso(Permisos.Sedes.Gestionar)).toBe(false);
    expect(store.tieneAlguno([Permisos.Sedes.Gestionar, Permisos.Usuarios.Ver])).toBe(true);
  });

  it('verifica una sola vez aunque varios guards pregunten a la vez', async () => {
    const a = store.asegurarVerificada();
    const b = store.asegurarVerificada();
    servidor.expectOne('/api/cuenta/sesion').flush(sesionAdmin);
    await Promise.all([a, b]);

    await store.asegurarVerificada();
    servidor.expectNone('/api/cuenta/sesion');
  });
});
