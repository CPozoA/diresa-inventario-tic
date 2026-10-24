import { EstablecimientoDto } from '../../../api/models/establecimiento-dto';
import { filtrarEstablecimientos } from './filtro-establecimientos';

const establecimiento = (codigoUnico: number, nombre: string, activo = true, distrito: string | null = null): EstablecimientoDto => ({
  id: String(codigoUnico),
  sedeId: 's1',
  sedeNombre: 'Red de Salud Huanta',
  idEstablecimiento: codigoUnico + 1000,
  codigoUnico,
  nombre,
  categoria: 'I-2',
  microred: 'Microred Luricocha',
  provincia: 'Huanta',
  distrito,
  ubigeo: '050601',
  activo,
});

const lista = [
  establecimiento(2345, 'Puesto de Salud Ccano'),
  establecimiento(2350, 'Centro de Salud Huanta', true, 'Huanta'),
  establecimiento(7001, 'Puesto de Salud Ayahuanco', false, 'Ayahuanco'),
];

describe('filtro de establecimientos', () => {
  it('sin texto muestra los activos', () => {
    expect(filtrarEstablecimientos(lista, '', 'activos').map((e) => e.codigoUnico)).toEqual([2345, 2350]);
  });

  it('busca por código único, por nombre sin tildes y por distrito', () => {
    expect(filtrarEstablecimientos(lista, '2345', 'todos').map((e) => e.nombre)).toEqual(['Puesto de Salud Ccano']);
    expect(filtrarEstablecimientos(lista, 'centro de salud', 'todos')).toHaveLength(1);
    expect(filtrarEstablecimientos(lista, 'AYAHUANCO', 'todos').map((e) => e.codigoUnico)).toEqual([7001]);
  });

  it('respeta el filtro de estado', () => {
    expect(filtrarEstablecimientos(lista, 'puesto', 'activos')).toHaveLength(1);
    expect(filtrarEstablecimientos(lista, 'puesto', 'inactivos').map((e) => e.codigoUnico)).toEqual([7001]);
  });
});
