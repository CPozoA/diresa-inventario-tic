import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { FichaActivoDto } from '../../../api/models/ficha-activo-dto';
import {
  ValorFormularioActivo,
  fichaDesdeFormulario,
  formularioDesdeFicha,
  hayDatosQueSeDescartan,
  hoyIso,
  opcionesDeCatalogo,
  totalDiscosGb,
  totalRamGb,
} from './formulario-activo';

function valor(): ValorFormularioActivo {
  return {
    generales: {
      codigoPatrimonial: '  pat-0105 ',
      tipoActivoId: 't1',
      marcaId: 'm1',
      modelo: ' Latitude 5440 ',
      numeroSerie: '',
      descripcion: '   ',
      areaId: 'a1',
      ubicacionFisica: 'Piso 2',
      responsable: '',
      estadoActivoId: 'e1',
      conservacion: 'Bueno',
      criticidad: null,
      observaciones: '',
    },
    adquisicion: {
      fechaAdquisicion: '2023-03-15',
      proveedor: '',
      numeroOrdenCompra: '',
      tipoAdquisicion: 'Compra',
      valorAdquisicion: 3850,
      vidaUtilAnios: 4,
      finGarantia: '',
    },
    tecnicos: {
      procesadorId: 'cpu1',
      generacionProcesador: '13',
      nucleos: 10,
      tipoGrafica: 'Integrada',
      modeloGrafica: 'Intel UHD',
      pantalla: 'otra',
      pantallaOtra: 14.5,
      sistemaOperativoId: 'so1',
      versionSistemaOperativo: '',
      nombreEquipo: 'ADM-LT-014',
      dominioGrupo: '',
      direccionIp: '192.168.10.45',
      direccionMac: '',
      antivirusId: 'av1',
      discoCifrado: true,
      puertos: ['UsbA', 'Hdmi'],
      accesorios: ['Cargador'],
    },
    discos: [
      { ranura: 'M.2', tipo: 'Nvme', capacidad: 512, capacidadOtra: null, marcaModelo: '', numeroSerie: '', uso: 'Sistema' },
      { ranura: '', tipo: 'Hdd', capacidad: 'otra', capacidadOtra: 750, marcaModelo: '', numeroSerie: '', uso: 'Datos' },
    ],
    modulosRam: [{ ranura: 'A', tipo: 'Ddr4', capacidadGb: 8, velocidadMhz: 3200, marcaModelo: '' }],
  };
}

describe('formulario del activo', () => {
  it('arma el cuerpo de la API con textos limpios y vacíos como null', () => {
    const cuerpo = fichaDesdeFormulario(valor(), 'est1', 'Computo');

    expect(cuerpo.establecimientoId).toBe('est1');
    expect(cuerpo.generales.codigoPatrimonial).toBe('pat-0105');
    expect(cuerpo.generales.modelo).toBe('Latitude 5440');
    expect(cuerpo.generales.numeroSerie).toBeNull();
    expect(cuerpo.generales.descripcion).toBeNull();
    expect(cuerpo.adquisicion?.finGarantia).toBeNull();
    expect(cuerpo.tecnicos?.procesadorId).toBe('cpu1');
    expect(cuerpo.tecnicos?.generacionProcesador).toBe('13');
    expect(cuerpo.tecnicos?.discoCifrado).toBe(true);
    expect(cuerpo.tecnicos?.antivirusId).toBe('av1');
    expect(cuerpo.tecnicos?.puertos).toEqual(['UsbA', 'Hdmi']);
    expect(cuerpo.tecnicos?.accesorios).toEqual(['Cargador']);
  });

  it('el modelo de la gráfica solo se envía si es dedicada, y la pantalla "Otra" usa la medida escrita', () => {
    const integrada = fichaDesdeFormulario(valor(), 'est1', 'Computo');
    const v = valor();
    v.tecnicos.tipoGrafica = 'Dedicada';
    const dedicada = fichaDesdeFormulario(v, 'est1', 'Computo');

    expect(integrada.tecnicos?.modeloGrafica).toBeNull();
    expect(dedicada.tecnicos?.modeloGrafica).toBe('Intel UHD');
    expect(integrada.tecnicos?.pantallaPulgadas).toBe(14.5);
  });

  it('usa la capacidad escrita cuando se eligió "Otra"', () => {
    const cuerpo = fichaDesdeFormulario(valor(), 'est1', 'Computo');

    expect(cuerpo.discos?.map((d) => d.capacidadGb)).toEqual([512, 750]);
    expect(cuerpo.modulosRam?.[0]).toMatchObject({ tipo: 'Ddr4', capacidadGb: 8, velocidadMhz: 3200 });
  });

  it('con ficha Red solo envía nombre de equipo, IP y MAC, sin discos ni RAM', () => {
    const cuerpo = fichaDesdeFormulario(valor(), 'est1', 'Red');

    expect(cuerpo.tecnicos).toMatchObject({ nombreEquipo: 'ADM-LT-014', direccionIp: '192.168.10.45', direccionMac: null });
    expect(cuerpo.tecnicos?.procesadorId).toBeNull();
    expect(cuerpo.tecnicos?.generacionProcesador).toBeNull();
    expect(cuerpo.tecnicos?.sistemaOperativoId).toBeNull();
    expect(cuerpo.tecnicos?.puertos).toBeNull();
    expect(cuerpo.tecnicos?.antivirusId).toBeNull();
    expect(cuerpo.discos).toEqual([]);
    expect(cuerpo.modulosRam).toEqual([]);
  });

  it('con ficha Ninguna no envía datos técnicos', () => {
    const cuerpo = fichaDesdeFormulario(valor(), 'est1', 'Ninguna');

    expect(cuerpo.tecnicos).toBeNull();
    expect(cuerpo.discos).toEqual([]);
    expect(cuerpo.modulosRam).toEqual([]);
  });

  it('avisa cuando el tipo elegido descartará datos ya escritos', () => {
    expect(hayDatosQueSeDescartan(valor(), 'Computo')).toBe(false);
    expect(hayDatosQueSeDescartan(valor(), 'Red')).toBe(true);

    const soloRed = valor();
    soloRed.tecnicos = {
      ...soloRed.tecnicos,
      procesadorId: null,
      generacionProcesador: null,
      nucleos: null,
      sistemaOperativoId: null,
      discoCifrado: null,
      tipoGrafica: null,
      modeloGrafica: '',
      pantalla: null,
      antivirusId: null,
      puertos: [],
      accesorios: [],
    };
    soloRed.discos = [];
    soloRed.modulosRam = [];
    expect(hayDatosQueSeDescartan(soloRed, 'Red')).toBe(false);
    expect(hayDatosQueSeDescartan(soloRed, 'Ninguna')).toBe(true);
  });

  it('suma discos y RAM', () => {
    expect(totalDiscosGb(valor().discos)).toBe(1262);
    expect(totalRamGb(valor().modulosRam)).toBe(8);
  });

  it('ofrece los elementos activos y el ya elegido aunque esté desactivado', () => {
    const lista: ElementoCatalogoDto[] = [
      { id: 'a', nombre: 'HP', activo: true, ficha: null, generaciones: null, grupo: null },
      { id: 'b', nombre: 'Compaq', activo: false, ficha: null, generaciones: null, grupo: null },
      { id: 'c', nombre: 'Acer', activo: false, ficha: null, generaciones: null, grupo: null },
    ];

    expect(opcionesDeCatalogo(lista, null).map((e) => e.id)).toEqual(['a']);
    expect(opcionesDeCatalogo(lista, 'b').map((e) => e.id)).toEqual(['a', 'b']);
  });

  it('carga una ficha para editar; una capacidad fuera de la lista pasa a "Otra"', () => {
    const ficha = {
      id: 'x',
      ficha: 'Computo',
      sedeId: 's1',
      sedeNombre: 'Red de Salud Huanta',
      establecimiento: { id: 'e1', codigoUnico: 2345, nombre: 'C.S. Huanta', categoria: null, microred: null, provincia: null, distrito: null },
      generales: {
        codigoPatrimonial: 'PAT-0001',
        tipoActivo: { id: 't1', nombre: 'Laptop' },
        marca: { id: 'm1', nombre: 'Dell' },
        modelo: 'Latitude',
        numeroSerie: null,
        descripcion: null,
        area: { id: 'a1', nombre: 'TI' },
        ubicacionFisica: null,
        responsable: null,
        estado: { id: 'e1', nombre: 'Operativo' },
        conservacion: 'Bueno',
        criticidad: 'Alta',
        observaciones: null,
      },
      adquisicion: {
        fechaAdquisicion: '2023-03-15',
        proveedor: null,
        numeroOrdenCompra: null,
        tipoAdquisicion: null,
        valorAdquisicion: '3850.5',
        vidaUtilAnios: '4',
        finGarantia: null,
      },
      tecnicos: {
        procesador: { id: 'cpu1', nombre: 'Intel Core i7' },
        generacion: { valor: '13', texto: '13.ª generación' },
        nucleos: null,
        tipoGrafica: 'Dedicada',
        modeloGrafica: 'NVIDIA T550',
        pantallaPulgadas: '15.6',
        sistemaOperativo: { id: 'so1', nombre: 'Windows 11 Pro' },
        versionSistemaOperativo: null,
        nombreEquipo: null,
        dominioGrupo: null,
        direccionIp: null,
        direccionMac: null,
        antivirus: { id: 'av1', nombre: 'Microsoft Defender' },
        discoCifrado: null,
        puertos: [{ valor: 'UsbA', texto: 'USB-A' }],
        accesorios: [],
      },
      discos: [
        { ranura: null, tipo: 'Nvme', capacidadGb: 512, marcaModelo: null, numeroSerie: null, uso: 'Sistema' },
        { ranura: null, tipo: 'Hdd', capacidadGb: 750, marcaModelo: null, numeroSerie: null, uso: 'Datos' },
      ],
      modulosRam: [{ ranura: 'A', tipo: 'Ddr4', capacidadGb: '8', velocidadMhz: null, marcaModelo: null }],
      ramTotalGb: 8,
      almacenamientoTotalGb: 1262,
      situacion: { antiguedadAnios: 3.6, garantia: 'SinDato', vidaUtilRestanteAnios: 0.4, renovacion: 'ProximoARenovar' },
      creadoEn: '2026-10-20T15:00:00Z',
      modificadoEn: null,
    } as FichaActivoDto;

    const v = formularioDesdeFicha(ficha, [256, 512, 1000], [14, 15.6]);

    expect(v.generales.criticidad).toBe('Alta');
    expect(v.generales.numeroSerie).toBe('');
    expect(v.adquisicion.valorAdquisicion).toBe(3850.5);
    expect(v.adquisicion.vidaUtilAnios).toBe(4);
    expect(v.tecnicos.sistemaOperativoId).toBe('so1');
    expect(v.tecnicos.procesadorId).toBe('cpu1');
    expect(v.tecnicos.generacionProcesador).toBe('13');
    expect(v.discos.map((d) => [d.capacidad, d.capacidadOtra])).toEqual([
      [512, null],
      ['otra', 750],
    ]);
    expect(v.modulosRam[0].capacidadGb).toBe(8);
    expect(v.tecnicos.pantalla).toBe(15.6);
    expect(v.tecnicos.antivirusId).toBe('av1');
    expect(v.tecnicos.puertos).toEqual(['UsbA']);
    expect(v.tecnicos.tipoGrafica).toBe('Dedicada');
  });

  it('da la fecha de hoy en la hora local', () => {
    expect(hoyIso(new Date(2026, 9, 5, 23, 30))).toBe('2026-10-05');
  });
});
