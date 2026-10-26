import { FotoActivoDto } from '../../../api/models/foto-activo-dto';
import {
  FotoEnFormulario,
  MAXIMO_FOTOS,
  agregarFotos,
  describirFoto,
  formatoTamano,
  fotosParaFormulario,
  marcarPrincipal,
  medidasReducidas,
  nombreFoto,
  ordenarFotos,
  planDeFotos,
  planVacio,
  quitarFoto,
  urlFoto,
} from './fotos';
import { esHeic, pareceImagen } from './reducir-imagen';

function dto(id: string, orden: number, principal = false, descripcion: string | null = null): FotoActivoDto {
  return { id, orden, principal, descripcion, tamanoBytes: 300_000, creadoEn: '2026-10-20T10:00:00-05:00' };
}

function nueva(clave: string): FotoEnFormulario {
  const imagen = { foto: new Blob(['f']), miniatura: new Blob(['m']), tamanoOriginal: 4_000_000 };
  return {
    clave,
    id: null,
    descripcion: '',
    descripcionGuardada: '',
    principal: false,
    tamanoBytes: 1,
    imagen,
    vista: `blob:${clave}`,
    estado: 'pendiente',
    error: null,
  };
}

const guardadas = [dto('a', 0, true, 'Vista frontal'), dto('b', 1), dto('c', 2, false, 'Placa')];
const enFormulario = () => fotosParaFormulario(guardadas, (f) => `/mini/${f.id}`);

describe('fotos de los activos', () => {
  it('reduce el lado mayor sin agrandar las pequeñas', () => {
    expect(medidasReducidas(4000, 3000, 1600)).toEqual({ ancho: 1600, alto: 1200 });
    expect(medidasReducidas(3000, 4000, 1600)).toEqual({ ancho: 1200, alto: 1600 });
    expect(medidasReducidas(800, 600, 1600)).toEqual({ ancho: 800, alto: 600 });
    expect(medidasReducidas(4032, 3024, 320)).toEqual({ ancho: 320, alto: 240 });
  });

  it('muestra los tamaños en KB o MB', () => {
    expect(formatoTamano(312 * 1024)).toBe('312 KB');
    expect(formatoTamano(4.1 * 1024 * 1024)).toBe('4.1 MB');
    expect(formatoTamano(100)).toBe('1 KB');
  });

  it('arma la dirección de la imagen y de la miniatura', () => {
    expect(urlFoto('', 'act', 'fot')).toBe('/api/activos/act/fotos/fot/archivo');
    expect(urlFoto('https://x/', 'act', 'fot', true)).toBe('https://x/api/activos/act/fotos/fot/archivo?miniatura=true');
  });

  it('ordena con la principal primero y luego por orden de subida', () => {
    expect(ordenarFotos([dto('x', 2), dto('y', 0), dto('z', 1, true)]).map((f) => f.id)).toEqual(['z', 'y', 'x']);
  });

  it('nombra las fotos sin descripción por su posición', () => {
    expect(nombreFoto('  Placa ', 1)).toBe('Placa');
    expect(nombreFoto(null, 3)).toBe('Foto 3');
  });

  it('reconoce las imágenes por el tipo o la extensión', () => {
    expect(pareceImagen({ type: 'image/jpeg', name: 'x.jpg' })).toBe(true);
    expect(pareceImagen({ type: 'application/pdf', name: 'factura.pdf' })).toBe(false);
    expect(pareceImagen({ type: '', name: 'IMG_0001.HEIC' })).toBe(true);
    expect(esHeic({ type: 'image/heic', name: 'a' })).toBe(true);
    expect(esHeic({ type: '', name: 'foto.jpg' })).toBe(false);
  });
});

describe('fotos en el formulario', () => {
  it('la primera foto nueva de un activo sin fotos queda como principal', () => {
    const lista = agregarFotos([], [nueva('n1'), nueva('n2')]);
    expect(lista.map((f) => f.principal)).toEqual([true, false]);
  });

  it('no pasa del máximo', () => {
    const lista = agregarFotos(enFormulario(), [nueva('n1'), nueva('n2'), nueva('n3'), nueva('n4')]);
    expect(lista).toHaveLength(MAXIMO_FOTOS);
  });

  it('al quitar la principal pasa a serlo la siguiente', () => {
    const lista = quitarFoto(enFormulario(), 'a');
    expect(lista.find((f) => f.principal)?.clave).toBe('b');
  });

  it('marca una sola principal y recorta la descripción a 60 caracteres', () => {
    const lista = describirFoto(marcarPrincipal(enFormulario(), 'c'), 'b', 'x'.repeat(80));
    expect(lista.filter((f) => f.principal).map((f) => f.clave)).toEqual(['c']);
    expect(lista.find((f) => f.clave === 'b')?.descripcion).toHaveLength(60);
  });

  it('sin cambios no hay nada que hacer', () => {
    expect(planVacio(planDeFotos(guardadas, enFormulario()))).toBe(true);
  });

  it('calcula qué eliminar, describir, subir y cuál fijar como principal', () => {
    let lista = quitarFoto(enFormulario(), 'b');
    lista = describirFoto(lista, 'c', '  Placa patrimonial ');
    lista = agregarFotos(lista, [nueva('n1')]);
    lista = marcarPrincipal(lista, 'n1');

    const plan = planDeFotos(guardadas, lista);

    expect(plan.eliminar).toEqual(['b']);
    expect(plan.describir).toEqual([{ id: 'c', descripcion: 'Placa patrimonial' }]);
    expect(plan.subir).toEqual(['n1']);
    expect(plan.principal).toBe('n1');
  });

  it('borrar la descripción la envía vacía', () => {
    const plan = planDeFotos(guardadas, describirFoto(enFormulario(), 'a', '   '));
    expect(plan.describir).toEqual([{ id: 'a', descripcion: null }]);
  });

  it('si se quita la principal guardada, se fija la que el usuario ve como principal', () => {
    const plan = planDeFotos(guardadas, quitarFoto(enFormulario(), 'a'));
    expect(plan.eliminar).toEqual(['a']);
    expect(plan.principal).toBe('b');
  });

  it('una nueva que es principal siempre se fija, aunque no hubiera fotos guardadas', () => {
    const lista = marcarPrincipal(agregarFotos([], [nueva('n1'), nueva('n2')]), 'n2');
    expect(planDeFotos([], lista).principal).toBe('n2');
  });
});
