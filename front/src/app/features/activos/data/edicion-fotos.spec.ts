import { Api } from '../../../api/api';
import { FotoActivoDto } from '../../../api/models/foto-activo-dto';
import { EdicionFotos } from './edicion-fotos';
import { ImagenReducida } from './fotos';
import { ErrorImagen } from './reducir-imagen';

/** API en memoria: guarda lo que se le pide y puede fallar la subida de un archivo. */
class ApiFalsa {
  fotos: FotoActivoDto[] = [];
  llamadas: string[] = [];
  fallarSubidaDe: Blob | null = null;
  private siguiente = 1;

  invoke(funcion: { name: string }, params: Record<string, unknown>): Promise<unknown> {
    const nombre = funcion.name;
    this.llamadas.push(nombre);
    const fotoId = params['fotoId'] as string | undefined;
    switch (nombre) {
      case 'listarFotosActivo':
        return Promise.resolve(structuredClone(this.fotos));
      case 'eliminarFotoActivo':
        this.fotos = this.fotos.filter((f) => f.id !== fotoId);
        return Promise.resolve();
      case 'describirFotoActivo': {
        const cuerpo = params['body'] as { descripcion: string | null };
        this.fotos = this.fotos.map((f) => (f.id === fotoId ? { ...f, descripcion: cuerpo.descripcion } : f));
        return Promise.resolve();
      }
      case 'marcarFotoPrincipalActivo':
        this.fotos = this.fotos.map((f) => ({ ...f, principal: f.id === fotoId }));
        return Promise.resolve();
      case 'subirFotoActivo': {
        const cuerpo = params['body'] as { archivo: Blob; descripcion?: string };
        if (cuerpo.archivo === this.fallarSubidaDe) {
          return Promise.reject(new Error('fallo'));
        }
        const foto: FotoActivoDto = {
          id: `s${this.siguiente++}`,
          orden: this.fotos.length,
          principal: this.fotos.length === 0,
          descripcion: cuerpo.descripcion ?? null,
          tamanoBytes: 1,
          creadoEn: '2026-10-25T10:00:00-05:00',
        };
        this.fotos = [...this.fotos, foto];
        return Promise.resolve(foto);
      }
      default:
        return Promise.reject(new Error(`inesperado: ${nombre}`));
    }
  }
}

function imagen(): ImagenReducida {
  return { foto: new Blob(['foto']), miniatura: new Blob(['mini']), tamanoOriginal: 4_000_000 };
}

function archivo(nombre: string): File {
  return new File(['x'], nombre, { type: nombre.endsWith('.pdf') ? 'application/pdf' : 'image/jpeg' });
}

describe('EdicionFotos', () => {
  let api: ApiFalsa;
  let edicion: EdicionFotos;
  const imagenes = new Map<string, ImagenReducida>();

  beforeEach(() => {
    api = new ApiFalsa();
    imagenes.clear();
    // jsdom no implementa las direcciones locales de archivos.
    URL.createObjectURL = vi.fn(() => 'blob:local');
    URL.revokeObjectURL = vi.fn();
    const reducir = (a: File): Promise<ImagenReducida> => {
      if (a.name.endsWith('.pdf')) {
        return Promise.reject(new ErrorImagen('No es una imagen. Use JPG, PNG, WebP o HEIC.'));
      }
      const i = imagen();
      imagenes.set(a.name, i);
      return Promise.resolve(i);
    };
    edicion = new EdicionFotos(api as unknown as Api, '', reducir);
  });

  it('agrega las imágenes y avisa de lo que no es imagen', async () => {
    await edicion.agregarArchivos([archivo('frente.jpg'), archivo('factura.pdf')]);

    expect(edicion.lista()).toHaveLength(1);
    expect(edicion.lista()[0].principal).toBe(true);
    expect(edicion.rechazadas()).toEqual([{ nombre: 'factura.pdf', motivo: 'No es una imagen. Use JPG, PNG, WebP o HEIC.' }]);
    expect(edicion.hayCambios()).toBe(true);
  });

  it('no agrega más de 6 y explica por qué', async () => {
    await edicion.agregarArchivos(Array.from({ length: 8 }, (_, i) => archivo(`f${i}.jpg`)));

    expect(edicion.lista()).toHaveLength(6);
    expect(edicion.rechazadas()).toHaveLength(2);
    expect(edicion.lugares()).toBe(0);
  });

  it('al guardar sube las nuevas con su descripción y fija la principal', async () => {
    await edicion.agregarArchivos([archivo('frente.jpg'), archivo('placa.jpg')]);
    const [, segunda] = edicion.lista();
    edicion.describir(segunda.clave, 'Placa patrimonial');
    edicion.marcarPrincipal(segunda.clave);

    const error = await edicion.aplicar('act');

    expect(error).toBeNull();
    expect(api.fotos.map((f) => [f.descripcion, f.principal])).toEqual([
      [null, false],
      ['Placa patrimonial', true],
    ]);
    expect(edicion.hayCambios()).toBe(false);
    expect(edicion.avance()).toBeNull();
  });

  it('al editar elimina primero, describe y no repite lo ya hecho', async () => {
    api.fotos = [
      { id: 'g1', orden: 0, principal: true, descripcion: 'Vista frontal', tamanoBytes: 1, creadoEn: '' },
      { id: 'g2', orden: 1, principal: false, descripcion: null, tamanoBytes: 1, creadoEn: '' },
    ];
    edicion.cargar('act', api.fotos);
    edicion.quitar('g1');
    edicion.describir('g2', 'Etiqueta');

    await edicion.aplicar('act');

    expect(api.llamadas.slice(0, 2)).toEqual(['eliminarFotoActivo', 'describirFotoActivo']);
    expect(api.fotos).toEqual([expect.objectContaining({ id: 'g2', descripcion: 'Etiqueta', principal: true })]);
    expect(edicion.hayCambios()).toBe(false);
  });

  it('si una subida falla la marca y al reintentar solo sube esa', async () => {
    await edicion.agregarArchivos([archivo('a.jpg'), archivo('b.jpg')]);
    api.fallarSubidaDe = imagenes.get('b.jpg')!.foto;

    const error = await edicion.aplicar('act');

    expect(error).not.toBeNull();
    expect(api.fotos).toHaveLength(1);
    expect(edicion.lista()[1].estado).toBe('error');
    expect(edicion.hayCambios()).toBe(true);

    api.fallarSubidaDe = null;
    api.llamadas = [];
    expect(await edicion.aplicar('act')).toBeNull();
    expect(api.llamadas.filter((l) => l === 'subirFotoActivo')).toHaveLength(1);
    expect(api.fotos).toHaveLength(2);
  });
});
