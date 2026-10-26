import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { buscarMismoNombre, coincidenciasAlEscribir, filtrarPorNombre, nombresParecidos, normalizarNombre } from './busqueda-catalogo';

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

  it('al escribir muestra primero los que empiezan así y luego los que lo contienen', () => {
    const procesadores = [elemento('AMD Ryzen 5'), elemento('Intel Core i5'), elemento('Core 2 Duo'), elemento('Intel Celeron')];

    expect(coincidenciasAlEscribir(procesadores, 'core').map((e) => e.nombre)).toEqual(['Core 2 Duo', 'Intel Core i5']);
    expect(coincidenciasAlEscribir(procesadores, '').length).toBe(4);
    expect(coincidenciasAlEscribir(lista, 'ALMACEN').map((e) => e.nombre)).toEqual(['Almacén central']);
  });

  it('avisa nombres parecidos antes de agregar uno nuevo', () => {
    const marcas = [elemento('Huawei Technologies'), elemento('HP'), elemento('Lenovo')];

    expect(nombresParecidos(marcas, 'huawei').map((e) => e.nombre)).toEqual(['Huawei Technologies']);
    expect(nombresParecidos(marcas, 'Hp')).toEqual([]);
    expect(nombresParecidos(marcas, 'Samsung')).toEqual([]);
  });
});
