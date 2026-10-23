import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { provideApiConfiguration } from '../../../api/api-configuration';
import { SesionDto } from '../../../api/models/sesion-dto';
import { IniciarSesionPage } from './iniciar-sesion.page';

const esperar = () => new Promise<void>((resolver) => setTimeout(resolver));

const sesion = (debeCambiar: boolean): SesionDto => ({
  usuarioId: 'u1',
  nombreUsuario: 'editor.huanta',
  nombreCompleto: 'Editor Huanta',
  correo: null,
  sede: null,
  esSuperAdmin: false,
  debeCambiarContrasena: debeCambiar,
  permisos: [],
});

describe('IniciarSesionPage', () => {
  let fixture: ComponentFixture<IniciarSesionPage>;
  let servidor: HttpTestingController;
  let navegar: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [IniciarSesionPage],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), provideApiConfiguration('')],
    });
    servidor = TestBed.inject(HttpTestingController);
    navegar = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    fixture = TestBed.createComponent(IniciarSesionPage);
    await fixture.whenStable();
  });

  afterEach(() => servidor.verify());

  async function ingresar(usuario: string, contrasena: string): Promise<void> {
    const elemento = fixture.nativeElement as HTMLElement;
    const [campoUsuario, campoClave] = Array.from(elemento.querySelectorAll('input'));
    campoUsuario.value = usuario;
    campoUsuario.dispatchEvent(new Event('input'));
    campoClave.value = contrasena;
    campoClave.dispatchEvent(new Event('input'));
    elemento.querySelector('form')?.dispatchEvent(new Event('submit'));
    await esperar();
  }

  it('muestra el mensaje de la API si las credenciales no son válidas', async () => {
    await ingresar('editor.huanta', 'incorrecta');
    servidor
      .expectOne('/api/cuenta/iniciar-sesion')
      .flush(
        { title: 'Usuario o contraseña incorrectos.', codigo: 'Cuenta.CredencialesInvalidas' },
        { status: 401, statusText: 'Unauthorized' },
      );
    await esperar();
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Usuario o contraseña incorrectos.');
    expect(navegar).not.toHaveBeenCalled();
  });

  it('con contraseña temporal lleva a cambiarla', async () => {
    await ingresar('editor.huanta', 'Temporal-2026x');
    servidor.expectOne('/api/cuenta/iniciar-sesion').flush(sesion(true));
    await esperar();
    // Con la sesión iniciada se pide un token antifalsificación nuevo (está ligado al usuario).
    servidor.expectOne('/api/cuenta/csrf').flush({ token: 't2' });
    await esperar();

    expect(navegar).toHaveBeenCalledWith('/cambiar-contrasena');
  });
});
