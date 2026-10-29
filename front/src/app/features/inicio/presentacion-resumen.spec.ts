import { ActivoAtencionDto } from '../../api/models/activo-atencion-dto';
import { CifrasRedDto } from '../../api/models/cifras-red-dto';
import {
  aniosEntre,
  barrasAntiguedad,
  colorGrupo,
  detalleAtencion,
  fechaCorta,
  filaRed,
  miles,
  ordenarRedes,
  porcentaje,
  tarjetas,
  totalRedes,
} from './presentacion-resumen';

function red(nombre: string, activos: number, renovar: number, completas: number, sinActivos: number, establecimientos: number): CifrasRedDto {
  return {
    sedeId: nombre,
    nombre,
    activos,
    enUso: Math.round(activos * 0.8),
    renovar,
    proximosARenovar: 1,
    garantiaVencida: 2,
    fichasEvaluadas: 100,
    fichasCompletas: completas,
    establecimientos,
    establecimientosSinActivos: sinActivos,
  };
}

function atencion(cambios: Partial<ActivoAtencionDto>): ActivoAtencionDto {
  return {
    id: 'a1',
    codigoPatrimonial: 'PAT-0045',
    tipoActivo: 'PC de escritorio',
    marca: 'HP',
    modelo: 'ProDesk 400 G4',
    establecimiento: 'Hospital de Huanta',
    estado: 'Operativo',
    motivo: 'Renovar',
    fechaAdquisicion: '2020-08-27',
    vidaUtilAnios: 5,
    fechaRenovacion: '2025-08-27',
    proximoMantenimiento: null,
    ...cambios,
  };
}

describe('presentación del resumen', () => {
  it('las cuatro tarjetas llevan su color y el filtro del inventario', () => {
    const lista = tarjetas({ activos: 248, enUso: 196, renovar: 21, proximosARenovar: 34, garantiaVencida: 92 });

    expect(lista.map((t) => [t.titulo, t.valor, t.color])).toEqual([
      ['Activos registrados', 248, 'azul'],
      ['Renovar', 21, 'rojo'],
      ['Próximos a renovar', 34, 'ambar'],
      ['Garantía vencida', 92, 'naranja'],
    ]);
    expect(lista[0].detalle).toBe('196 en uso (79%)');
    expect(lista[3].detalle).toBe('37% del inventario');
    expect(lista.map((t) => t.filtro)).toEqual([{}, { atencion: 'Renovar' }, { atencion: 'ProximoARenovar' }, { atencion: 'GarantiaVencida' }]);
  });

  it('los números llevan separador de miles y los porcentajes se redondean', () => {
    expect(miles(1630)).toBe('1,630');
    expect(porcentaje(1, 3)).toBe('33%');
    expect(porcentaje(5, 0)).toBe('—');
  });

  it('cada grupo de estados tiene su color', () => {
    expect(colorGrupo('EnUso')).toBe('verde');
    expect(colorGrupo('FueraDeServicio')).toBe('rojo');
    expect(colorGrupo('DeBaja')).toBe('gris');
    expect(colorGrupo(null)).toBe('gris');
  });

  it('la antigüedad se dibuja relativa al rango más grande, con "Sin fecha" atenuado', () => {
    const barras = barrasAntiguedad([
      { rango: 'MenosDe2', cantidad: 38 },
      { rango: 'De2A4', cantidad: 71 },
      { rango: 'MasDe6', cantidad: 33 },
      { rango: 'SinFecha', cantidad: 52 },
    ]);

    expect(barras[1].ancho).toBe(100);
    expect(barras[2].color).toBe('rojo');
    expect(barras[3]).toEqual(expect.objectContaining({ texto: 'Sin fecha', atenuado: true }));
  });

  it('explica por qué cada activo requiere atención', () => {
    const hoy = '2026-10-27';

    expect(detalleAtencion(atencion({}), hoy)).toBe('HP ProDesk 400 G4 · 6.2 años de uso y 5 de vida útil · Hospital de Huanta');
    expect(detalleAtencion(atencion({ motivo: 'ProximoARenovar', fechaRenovacion: '2027-04-27' }), hoy)).toBe(
      'HP ProDesk 400 G4 · le quedan 0.5 años de vida útil · Hospital de Huanta',
    );
    expect(detalleAtencion(atencion({ motivo: 'FueraDeServicio', estado: 'Inoperativo' }), hoy)).toBe(
      'HP ProDesk 400 G4 · Inoperativo · Hospital de Huanta',
    );
    expect(detalleAtencion(atencion({ motivo: 'MantenimientoVencido', proximoMantenimiento: '2026-10-10' }), hoy)).toBe(
      'HP ProDesk 400 G4 · preventivo vencido el 10/10/2026 · Hospital de Huanta',
    );
    expect(aniosEntre('2020-10-27', '2026-10-27')).toBe(6);
  });

  it('la tabla por red se ordena y suma, y marca las redes atrasadas en el registro', () => {
    const redes = [red('Huanta', 248, 21, 47, 23, 41), red('Huamanga', 412, 31, 66, 21, 68), red('Puquio', 188, 19, 58, 30, 47)];
    const filas = redes.map(filaRed);

    expect(ordenarRedes(filas, 'activos').map((f) => f.nombre)).toEqual(['Huamanga', 'Huanta', 'Puquio']);
    expect(ordenarRedes(filas, 'sinActivos').map((f) => f.nombre)).toEqual(['Puquio', 'Huanta', 'Huamanga']);
    expect(filas.find((f) => f.nombre === 'Huanta')?.atrasada).toBe(true);
    expect(filas.find((f) => f.nombre === 'Huamanga')?.atrasada).toBe(false);

    const total = totalRedes(filas, redes);
    expect(total.activos).toBe(848);
    expect(total.sinActivos).toBe(74);
    expect(total.completasPorcentaje).toBe(57);
  });

  it('muestra la fecha como dd/mm/aaaa', () => {
    expect(fechaCorta('2026-10-27')).toBe('27/10/2026');
  });
});
