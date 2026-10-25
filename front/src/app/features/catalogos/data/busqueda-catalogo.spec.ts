import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { buscarMismoNombre, filtrarPorNombre, normalizarNombre } from './busqueda-catalogo';

const elemento = (nombre: string, activo = true): ElementoCatalogoDto => ({ id: nombre, nombre, activo, ficha: null, generaciones: null });

const lista = [elemento('HP'), elemento('HP Inc.'), elemento('Lenovo'), elemento('Almacén central'), elemento('Dell', false)];

describe('búsqueda en catálogos', () => {
  it('compara sin mayúsculas, tildes ni espacios de más', () => {
    expect(normalizarNombre('  Almacén   CENTRAL ')).toBe('almacen central');
  });

  it('filtra por parte del nombre', () => {
    expect(filtrarPorNombre(lista, 'hp').map((e) => e.nombre)).toEqual(['HP', 'HP Inc.']);
    expect(filtrarPorNombre(lista, 'almacen').map((e) => e.nombre)).toEqual(['Almacén central']);
  });

  it('sin texto devuelve todo y sin coincidencias no devuelve nada', () => {
    expect(filtrarPorNombre(lista, '  ')).toHaveLength(lista.length);
    expect(filtrarPorNombre(lista, 'asus')).toHaveLength(0);
  });

  it('encuentra el mismo nombre, también si está desactivado', () => {
    expect(buscarMismoNombre(lista, 'hp')?.nombre).toBe('HP');
    expect(buscarMismoNombre(lista, 'DELL')?.activo).toBe(false);
  });

  it('una coincidencia parcial no cuenta como repetido', () => {
    expect(buscarMismoNombre(lista, 'Len')).toBeUndefined();
    expect(buscarMismoNombre(lista, '')).toBeUndefined();
  });
});
