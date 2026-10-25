import { ElementoCatalogoDto } from '../../../api/models/elemento-catalogo-dto';
import { FichaActivo } from '../../../api/models/ficha-activo';
import { FichaActivoDto } from '../../../api/models/ficha-activo-dto';
import { FichaTecnica } from '../../../api/models/ficha-tecnica';
import { aNumero } from './textos-activo';

/**
 * Valores del formulario del activo, tal como los tienen los campos (textos vacíos, números nulos…).
 * Las listas fijas se guardan como el texto que viaja en la API ("Nvme", "Ddr4").
 */
export interface ValorGenerales {
  codigoPatrimonial: string;
  tipoActivoId: string | null;
  marcaId: string | null;
  modelo: string;
  numeroSerie: string;
  descripcion: string;
  areaId: string | null;
  ubicacionFisica: string;
  responsable: string;
  estadoActivoId: string | null;
  conservacion: string | null;
  criticidad: string | null;
  observaciones: string;
}

export interface ValorAdquisicion {
  fechaAdquisicion: string;
  proveedor: string;
  numeroOrdenCompra: string;
  tipoAdquisicion: string | null;
  valorAdquisicion: number | null;
  vidaUtilAnios: number | null;
  finGarantia: string;
}

export interface ValorTecnicos {
  procesadorId: string | null;
  /** Código de la generación ("13"), de la lista de la familia del procesador (D44). */
  generacionProcesador: string | null;
  nucleos: number | null;
  tipoGrafica: string | null;
  /** Solo si la gráfica es dedicada. */
  modeloGrafica: string;
  /** Medida de la lista, u "otra" (se escribe en pantallaOtra). */
  pantalla: number | 'otra' | null;
  pantallaOtra: number | null;
  sistemaOperativoId: string | null;
  versionSistemaOperativo: string;
  nombreEquipo: string;
  dominioGrupo: string;
  direccionIp: string;
  direccionMac: string;
  antivirusId: string | null;
  discoCifrado: boolean | null;
  /** Valores de la lista de puertos ("UsbA", "Hdmi"…). */
  puertos: string[];
  /** Valores de la lista de accesorios ("Cargador"…). */
  accesorios: string[];
}

/** Capacidad elegida de la lista, u "otra" (se escribe en capacidadOtra). */
export type CapacidadDisco = number | 'otra' | null;

export interface ValorDisco {
  ranura: string;
  tipo: string;
  capacidad: CapacidadDisco;
  capacidadOtra: number | null;
  marcaModelo: string;
  numeroSerie: string;
  uso: string;
}

export interface ValorModuloRam {
  ranura: string;
  tipo: string;
  capacidadGb: number | null;
  velocidadMhz: number | null;
  marcaModelo: string;
}

export interface ValorFormularioActivo {
  generales: ValorGenerales;
  adquisicion: ValorAdquisicion;
  tecnicos: ValorTecnicos;
  discos: ValorDisco[];
  modulosRam: ValorModuloRam[];
}

/** Ficha técnica efectiva de un tipo: sin dato (no debería pasar) se trata como "Ninguna". */
export type Ficha = NonNullable<FichaTecnica>;

const texto = (valor: string | null | undefined): string | null => {
  const limpio = (valor ?? '').trim();
  return limpio === '' ? null : limpio;
};

const numero = (valor: number | null | undefined): number | null =>
  valor === null || valor === undefined || Number.isNaN(valor) ? null : valor;

/** Pulgadas de la pantalla (de la lista o escritas). */
export function pulgadasPantalla(t: Pick<ValorTecnicos, 'pantalla' | 'pantallaOtra'>): number | null {
  return t.pantalla === 'otra' ? numero(t.pantallaOtra) : numero(t.pantalla);
}

/** Capacidad en GB de un disco del formulario (de la lista o escrita). */
export function capacidadDisco(disco: Pick<ValorDisco, 'capacidad' | 'capacidadOtra'>): number | null {
  return disco.capacidad === 'otra' ? numero(disco.capacidadOtra) : numero(disco.capacidad);
}

/**
 * Cuerpo para la API a partir del formulario. Los textos van sin espacios a los lados (vacíos como null) y solo se
 * envían las características que lleva la ficha del tipo (D43): lo escrito en secciones ocultas no se guarda.
 */
export function fichaDesdeFormulario(valor: ValorFormularioActivo, establecimientoId: string, ficha: Ficha): FichaActivo {
  const g = valor.generales;
  const a = valor.adquisicion;
  const t = valor.tecnicos;

  const deRed = {
    nombreEquipo: texto(t.nombreEquipo),
    direccionIp: texto(t.direccionIp),
    direccionMac: texto(t.direccionMac),
  };

  const tecnicos =
    ficha === 'Computo'
      ? {
          procesadorId: t.procesadorId,
          generacionProcesador: t.procesadorId ? t.generacionProcesador : null,
          nucleos: numero(t.nucleos),
          tipoGrafica: t.tipoGrafica as NonNullable<FichaActivo['tecnicos']>['tipoGrafica'],
          modeloGrafica: t.tipoGrafica === 'Dedicada' ? texto(t.modeloGrafica) : null,
          pantallaPulgadas: pulgadasPantalla(t),
          sistemaOperativoId: t.sistemaOperativoId,
          versionSistemaOperativo: texto(t.versionSistemaOperativo),
          ...deRed,
          dominioGrupo: texto(t.dominioGrupo),
          antivirusId: t.antivirusId,
          discoCifrado: t.discoCifrado,
          puertos: t.puertos as NonNullable<NonNullable<FichaActivo['tecnicos']>['puertos']>,
          accesorios: t.accesorios as NonNullable<NonNullable<FichaActivo['tecnicos']>['accesorios']>,
        }
      : ficha === 'Red'
        ? {
            procesadorId: null,
            generacionProcesador: null,
            nucleos: null,
            tipoGrafica: null,
            modeloGrafica: null,
            pantallaPulgadas: null,
            sistemaOperativoId: null,
            versionSistemaOperativo: null,
            ...deRed,
            dominioGrupo: null,
            antivirusId: null,
            discoCifrado: null,
            puertos: null,
            accesorios: null,
          }
        : null;

  const esComputo = ficha === 'Computo';

  return {
    establecimientoId,
    generales: {
      codigoPatrimonial: g.codigoPatrimonial.trim(),
      tipoActivoId: g.tipoActivoId ?? '',
      marcaId: g.marcaId ?? '',
      modelo: g.modelo.trim(),
      numeroSerie: texto(g.numeroSerie),
      descripcion: texto(g.descripcion),
      areaId: g.areaId ?? '',
      ubicacionFisica: texto(g.ubicacionFisica),
      responsable: texto(g.responsable),
      estadoActivoId: g.estadoActivoId ?? '',
      conservacion: g.conservacion as FichaActivo['generales']['conservacion'],
      criticidad: g.criticidad as FichaActivo['generales']['criticidad'],
      observaciones: texto(g.observaciones),
    },
    adquisicion: {
      fechaAdquisicion: texto(a.fechaAdquisicion),
      proveedor: texto(a.proveedor),
      numeroOrdenCompra: texto(a.numeroOrdenCompra),
      tipoAdquisicion: a.tipoAdquisicion as NonNullable<FichaActivo['adquisicion']>['tipoAdquisicion'],
      valorAdquisicion: numero(a.valorAdquisicion),
      vidaUtilAnios: numero(a.vidaUtilAnios),
      finGarantia: texto(a.finGarantia),
    },
    tecnicos,
    discos: esComputo
      ? valor.discos.map((d) => ({
          ranura: texto(d.ranura),
          tipo: d.tipo as NonNullable<FichaActivo['discos']>[number]['tipo'],
          capacidadGb: capacidadDisco(d) ?? 0,
          marcaModelo: texto(d.marcaModelo),
          numeroSerie: texto(d.numeroSerie),
          uso: d.uso as NonNullable<FichaActivo['discos']>[number]['uso'],
        }))
      : [],
    modulosRam: esComputo
      ? valor.modulosRam.map((m) => ({
          ranura: texto(m.ranura),
          tipo: m.tipo as NonNullable<FichaActivo['modulosRam']>[number]['tipo'],
          capacidadGb: m.capacidadGb ?? 0,
          velocidadMhz: numero(m.velocidadMhz),
          marcaModelo: texto(m.marcaModelo),
        }))
      : [],
  };
}

/** Valores del formulario para editar una ficha existente. */
export function formularioDesdeFicha(
  ficha: FichaActivoDto,
  capacidadesDisco: readonly number[],
  pantallas: readonly number[] = [],
): ValorFormularioActivo {
  const g = ficha.generales;
  const a = ficha.adquisicion;
  const t = ficha.tecnicos;
  const pulgadas = aNumero(t.pantallaPulgadas);
  const pantallaEnLista = pulgadas !== null && pantallas.includes(pulgadas);
  return {
    generales: {
      codigoPatrimonial: g.codigoPatrimonial,
      tipoActivoId: g.tipoActivo.id,
      marcaId: g.marca.id,
      modelo: g.modelo,
      numeroSerie: g.numeroSerie ?? '',
      descripcion: g.descripcion ?? '',
      areaId: g.area.id,
      ubicacionFisica: g.ubicacionFisica ?? '',
      responsable: g.responsable ?? '',
      estadoActivoId: g.estado.id,
      conservacion: g.conservacion,
      criticidad: g.criticidad ?? null,
      observaciones: g.observaciones ?? '',
    },
    adquisicion: {
      fechaAdquisicion: a.fechaAdquisicion ?? '',
      proveedor: a.proveedor ?? '',
      numeroOrdenCompra: a.numeroOrdenCompra ?? '',
      tipoAdquisicion: a.tipoAdquisicion ?? null,
      valorAdquisicion: aNumero(a.valorAdquisicion),
      vidaUtilAnios: aNumero(a.vidaUtilAnios),
      finGarantia: a.finGarantia ?? '',
    },
    tecnicos: {
      procesadorId: t.procesador?.id ?? null,
      generacionProcesador: t.generacion?.valor ?? null,
      nucleos: aNumero(t.nucleos),
      tipoGrafica: t.tipoGrafica ?? null,
      modeloGrafica: t.modeloGrafica ?? '',
      pantalla: pulgadas === null ? null : pantallaEnLista ? pulgadas : 'otra',
      pantallaOtra: pulgadas !== null && !pantallaEnLista ? pulgadas : null,
      sistemaOperativoId: t.sistemaOperativo?.id ?? null,
      versionSistemaOperativo: t.versionSistemaOperativo ?? '',
      nombreEquipo: t.nombreEquipo ?? '',
      dominioGrupo: t.dominioGrupo ?? '',
      direccionIp: t.direccionIp ?? '',
      direccionMac: t.direccionMac ?? '',
      antivirusId: t.antivirus?.id ?? null,
      discoCifrado: t.discoCifrado ?? null,
      puertos: (t.puertos ?? []).map((p) => p.valor),
      accesorios: (t.accesorios ?? []).map((a) => a.valor),
    },
    discos: ficha.discos.map((d) => {
      const gb = aNumero(d.capacidadGb);
      const enLista = gb !== null && capacidadesDisco.includes(gb);
      return {
        ranura: d.ranura ?? '',
        tipo: d.tipo ?? 'SsdSata',
        capacidad: enLista ? gb : 'otra',
        capacidadOtra: enLista ? null : gb,
        marcaModelo: d.marcaModelo ?? '',
        numeroSerie: d.numeroSerie ?? '',
        uso: d.uso ?? 'Datos',
      };
    }),
    modulosRam: ficha.modulosRam.map((m) => ({
      ranura: m.ranura ?? '',
      tipo: m.tipo ?? 'Ddr4',
      capacidadGb: aNumero(m.capacidadGb),
      velocidadMhz: aNumero(m.velocidadMhz),
      marcaModelo: m.marcaModelo ?? '',
    })),
  };
}

/** Si el formulario tiene datos que la ficha elegida no guardará (al cambiar, por ejemplo, de Laptop a Monitor). */
export function hayDatosQueSeDescartan(valor: ValorFormularioActivo, ficha: Ficha): boolean {
  if (ficha === 'Computo') {
    return false;
  }
  const t = valor.tecnicos;
  const deComputo = [t.modeloGrafica, t.versionSistemaOperativo, t.dominioGrupo].some((x) => x.trim() !== '');
  const otros =
    t.procesadorId !== null ||
    t.nucleos !== null ||
    t.tipoGrafica !== null ||
    pulgadasPantalla(t) !== null ||
    t.antivirusId !== null ||
    t.puertos.length > 0 ||
    t.accesorios.length > 0 ||
    t.sistemaOperativoId !== null ||
    t.discoCifrado !== null ||
    valor.discos.length > 0 ||
    valor.modulosRam.length > 0;
  const deRed = [t.nombreEquipo, t.direccionIp, t.direccionMac].some((x) => x.trim() !== '');
  return deComputo || otros || (ficha === 'Ninguna' && deRed);
}

/** Total en GB de los discos con capacidad. */
export function totalDiscosGb(discos: readonly Pick<ValorDisco, 'capacidad' | 'capacidadOtra'>[]): number {
  return discos.reduce((suma, d) => suma + (capacidadDisco(d) ?? 0), 0);
}

/** Total en GB de los módulos de RAM con capacidad. */
export function totalRamGb(modulos: readonly Pick<ValorModuloRam, 'capacidadGb'>[]): number {
  return modulos.reduce((suma, m) => suma + (m.capacidadGb ?? 0), 0);
}

/**
 * Elementos de un catálogo que se ofrecen en un campo: los activos, y el ya elegido aunque se haya desactivado
 * (la API lo acepta si el activo ya lo tenía, para poder editar sus otros datos).
 */
export function opcionesDeCatalogo(
  elementos: readonly ElementoCatalogoDto[],
  elegidoId: string | null | undefined,
): ElementoCatalogoDto[] {
  return elementos.filter((e) => e.activo || e.id === elegidoId);
}

/** Hoy como "aaaa-mm-dd" en la hora local (para el máximo de la fecha de adquisición). */
export function hoyIso(hoy: Date = new Date()): string {
  const dos = (n: number) => String(n).padStart(2, '0');
  return `${hoy.getFullYear()}-${dos(hoy.getMonth() + 1)}-${dos(hoy.getDate())}`;
}
