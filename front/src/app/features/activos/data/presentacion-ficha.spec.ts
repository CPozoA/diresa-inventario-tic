import { FichaActivoDto } from '../../../api/models/ficha-activo-dto';
import { FichaTecnica } from '../../../api/models/ficha-tecnica';
import { cicloDeVida, datosTecnicos, detalleEstablecimiento } from './presentacion-ficha';

const ref = (nombre: string) => ({ id: nombre, nombre });

function ficha(tipo: FichaTecnica, cambios: Partial<FichaActivoDto> = {}): FichaActivoDto {
  return {
    id: 'a1',
    ficha: tipo,
    sedeId: 's1',
    sedeNombre: 'Red de Salud Huanta',
    establecimiento: {
      id: 'e1',
      codigoUnico: 2345,
      nombre: 'C.S. Huanta',
      categoria: 'I-4',
      microred: 'Microred Huanta',
      provincia: 'Huanta',
      distrito: 'Huanta',
    },
    generales: {
      codigoPatrimonial: 'PAT-0001',
      tipoActivo: ref('Laptop'),
      marca: ref('Dell'),
      modelo: 'Latitude 5440',
      numeroSerie: '7XK2P93',
      descripcion: null,
      area: ref('Administración'),
      ubicacionFisica: null,
      responsable: null,
      estado: ref('Operativo'),
      conservacion: 'Bueno',
      criticidad: 'Media',
      observaciones: null,
    },
    adquisicion: {
      fechaAdquisicion: '2023-03-15',
      proveedor: null,
      numeroOrdenCompra: null,
      tipoAdquisicion: 'Compra',
      valorAdquisicion: 3850,
      vidaUtilAnios: 4,
      finGarantia: '2024-03-14',
    },
    tecnicos: {
      procesador: ref('Intel Core i7'),
      generacion: { valor: '13', texto: '13.ª generación' },
      nucleos: 10,
      tipoGrafica: 'Dedicada',
      modeloGrafica: 'NVIDIA T550',
      pantallaPulgadas: '14',
      sistemaOperativo: ref('Windows 11 Pro'),
      versionSistemaOperativo: '23H2',
      nombreEquipo: 'ADM-LT-001',
      dominioGrupo: null,
      direccionIp: '192.168.10.45',
      direccionMac: 'A4:BB:6D:12:34:56',
      antivirus: { id: 'av1', nombre: 'Microsoft Defender' },
      discoCifrado: true,
      puertos: [
        { valor: 'UsbA', texto: 'USB-A' },
        { valor: 'Hdmi', texto: 'HDMI' },
      ],
      accesorios: [],
    },
    discos: [{ ranura: 'M.2 #1', tipo: 'Nvme', capacidadGb: 512, marcaModelo: null, numeroSerie: null, uso: 'Sistema' }],
    modulosRam: [
      { ranura: 'A', tipo: 'Ddr5', capacidadGb: 8, velocidadMhz: 5600, marcaModelo: null },
      { ranura: 'B', tipo: 'Ddr5', capacidadGb: 8, velocidadMhz: 5600, marcaModelo: null },
    ],
    ramTotalGb: 16,
    almacenamientoTotalGb: 512,
    situacion: { antiguedadAnios: 3.6, garantia: 'Vencida', vidaUtilRestanteAnios: 0.4, renovacion: 'ProximoARenovar' },
    creadoEn: '2026-10-20T15:00:00Z',
    modificadoEn: null,
    ...cambios,
  };
}

describe('presentación de la ficha', () => {
  it('Cómputo muestra todas las características con valor, con RAM y almacenamiento totales', () => {
    const datos = datosTecnicos(ficha('Computo'));
    const etiquetas = datos.map((d) => d.etiqueta);

    expect(etiquetas).toEqual([
      'Procesador',
      'Memoria RAM',
      'Almacenamiento',
      'Sistema operativo',
      'Nombre de equipo',
      'Dirección IP',
      'Dirección MAC',
      'Antivirus o EDR',
      'Disco cifrado',
      'Pantalla',
      'Tarjeta gráfica',
      'Puertos y conectividad',
    ]);
    expect(datos.find((d) => d.etiqueta === 'Tarjeta gráfica')?.valor).toBe('Dedicada · NVIDIA T550');
    expect(datos.find((d) => d.etiqueta === 'Puertos y conectividad')?.valor).toBe('USB-A, HDMI');
    expect(datos[0].valor).toBe('Intel Core i7 · 13.ª generación · 10 núcleos');
    expect(datos[1]).toMatchObject({ valor: '16 GB', detalle: '(2 módulos)' });
    expect(datos[2]).toMatchObject({ valor: '512 GB', detalle: '(1 disco)' });
    expect(datos[3].valor).toBe('Windows 11 Pro · 23H2');
    expect(datos.find((d) => d.etiqueta === 'Pantalla')?.valor).toBe('14 pulgadas');
  });

  it('Red muestra solo nombre de equipo, IP y MAC', () => {
    expect(datosTecnicos(ficha('Red')).map((d) => d.etiqueta)).toEqual(['Nombre de equipo', 'Dirección IP', 'Dirección MAC']);
  });

  it('Ninguna no muestra características técnicas', () => {
    expect(datosTecnicos(ficha('Ninguna'))).toEqual([]);
  });

  it('el ciclo de vida compara la antigüedad con la vida útil', () => {
    const ciclo = cicloDeVida(ficha('Computo'));

    expect(ciclo.porcentaje).toBeCloseTo(90);
    expect(ciclo.antiguedad).toBe('3.6');
    expect(ciclo.mensaje).toBe('Le quedan 0.4 años de vida útil');
    expect(ciclo.fechaRenovacion).toBe('2027-03-15');
  });

  it('pasada la vida útil la barra se llena y el mensaje lo dice', () => {
    const vencido = ficha('Computo', {
      situacion: { antiguedadAnios: 5.2, garantia: 'Vencida', vidaUtilRestanteAnios: -1.2, renovacion: 'Renovar' },
    });
    const ciclo = cicloDeVida(vencido);

    expect(ciclo.porcentaje).toBe(100);
    expect(ciclo.mensaje).toBe('Cumplió su vida útil hace 1.2 años');
  });

  it('sin fecha de adquisición no hay barra ni renovación prevista', () => {
    const sinFecha = ficha('Computo', {
      adquisicion: { ...ficha('Computo').adquisicion, fechaAdquisicion: null },
      situacion: { antiguedadAnios: null, garantia: 'SinDato', vidaUtilRestanteAnios: null, renovacion: 'SinDato' },
    });
    const ciclo = cicloDeVida(sinFecha);

    expect(ciclo.porcentaje).toBeNull();
    expect(ciclo.mensaje).toBeNull();
    expect(ciclo.fechaRenovacion).toBeNull();
  });

  it('resume el establecimiento en una línea', () => {
    expect(detalleEstablecimiento(ficha('Computo'))).toBe('Categoría I-4 · Microred Huanta · Huanta, Huanta · Red de Salud Huanta');
  });
});
