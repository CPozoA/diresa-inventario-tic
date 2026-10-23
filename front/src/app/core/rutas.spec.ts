import { rutaInternaSegura } from './rutas';

describe('rutaInternaSegura', () => {
  it('acepta rutas internas', () => {
    expect(rutaInternaSegura('/usuarios?buscar=ana')).toBe('/usuarios?buscar=ana');
  });

  it('rechaza direcciones externas (evita redirecciones abiertas)', () => {
    expect(rutaInternaSegura('https://otro-sitio.com')).toBe('/');
    expect(rutaInternaSegura('//otro-sitio.com')).toBe('/');
    expect(rutaInternaSegura('/\\otro-sitio.com')).toBe('/');
  });

  it('sin destino, o si el destino es el propio inicio de sesión, va al inicio', () => {
    expect(rutaInternaSegura(undefined)).toBe('/');
    expect(rutaInternaSegura('/iniciar-sesion?volver=/x')).toBe('/');
  });
});
