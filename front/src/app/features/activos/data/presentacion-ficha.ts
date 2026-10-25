import { FichaActivoDto } from '../../../api/models/ficha-activo-dto';
import { aNumero, cantidad, fechaRenovacion, formatoAnios, formatoGb, textoTipoGrafica } from './textos-activo';

/** Un dato de la ficha listo para mostrar. */
export interface DatoFicha {
  readonly etiqueta: string;
  readonly valor: string;
  /** Texto atenuado a continuación del valor: "(2 módulos)". */
  readonly detalle?: string;
  /** Se muestra en letra monoespaciada (IP, MAC, nombre de equipo). */
  readonly mono?: boolean;
}

const unir = (...partes: (string | number | null | undefined)[]): string =>
  partes.filter((p) => p !== null && p !== undefined && String(p).trim() !== '').join(' · ');

function dato(etiqueta: string, valor: string | null | undefined, extra: Partial<DatoFicha> = {}): DatoFicha | null {
  return valor === null || valor === undefined || valor.trim() === '' ? null : { etiqueta, valor, ...extra };
}

/**
 * Características técnicas que se muestran según la ficha del tipo (D43): todas en Cómputo, solo las de red en Red
 * y ninguna en los demás. Los datos vacíos no se muestran.
 */
export function datosTecnicos(ficha: FichaActivoDto): DatoFicha[] {
  if (ficha.ficha === 'Ninguna') {
    return [];
  }

  const t = ficha.tecnicos;
  const deRed = [
    dato('Nombre de equipo', t.nombreEquipo, { mono: true }),
    dato('Dirección IP', t.direccionIp, { mono: true }),
    dato('Dirección MAC', t.direccionMac, { mono: true }),
  ];

  if (ficha.ficha === 'Red') {
    return deRed.filter((d): d is DatoFicha => d !== null);
  }

  const nucleos = aNumero(t.nucleos);
  const pantalla = aNumero(t.pantallaPulgadas);
  const modulos = ficha.modulosRam.length;
  const discos = ficha.discos.length;
  const cifrado = t.discoCifrado === null || t.discoCifrado === undefined ? null : t.discoCifrado ? 'Sí' : 'No';

  return [
    dato('Procesador', unir(t.procesador?.nombre, t.generacion?.texto, nucleos === null ? null : cantidad(nucleos, 'núcleo', 'núcleos'))),
    modulos > 0 ? dato('Memoria RAM', formatoGb(ficha.ramTotalGb), { detalle: `(${cantidad(modulos, 'módulo', 'módulos')})` }) : null,
    discos > 0
      ? dato('Almacenamiento', formatoGb(ficha.almacenamientoTotalGb), { detalle: `(${cantidad(discos, 'disco', 'discos')})` })
      : null,
    dato('Sistema operativo', unir(t.sistemaOperativo?.nombre, t.versionSistemaOperativo)),
    deRed[0],
    dato('Dominio o grupo de trabajo', t.dominioGrupo),
    deRed[1],
    deRed[2],
    dato('Antivirus o EDR', t.antivirus?.nombre),
    dato('Disco cifrado', cifrado),
    dato('Pantalla', pantalla === null ? null : `${String(pantalla)} pulgadas`),
    dato('Tarjeta gráfica', unir(t.tipoGrafica ? textoTipoGrafica(t.tipoGrafica) : null, t.modeloGrafica)),
    dato('Puertos y conectividad', (t.puertos ?? []).map((p) => p.texto).join(', ')),
    dato('Accesorios incluidos', (t.accesorios ?? []).map((a) => a.texto).join(', ')),
  ].filter((d): d is DatoFicha => d !== null);
}

/** Antigüedad frente a la vida útil, para la barra del ciclo de vida. */
export interface CicloDeVida {
  readonly antiguedad: string;
  readonly vidaUtil: number | null;
  /** De 0 a 100. Nulo sin fecha de adquisición o sin vida útil. */
  readonly porcentaje: number | null;
  /** "Le quedan 0.4 años de vida útil", "Cumplió su vida útil hace 1.2 años"… */
  readonly mensaje: string | null;
  readonly fechaRenovacion: string | null;
}

export function cicloDeVida(ficha: FichaActivoDto): CicloDeVida {
  const antiguedad = aNumero(ficha.situacion.antiguedadAnios);
  const vidaUtil = aNumero(ficha.adquisicion.vidaUtilAnios);
  const restante = aNumero(ficha.situacion.vidaUtilRestanteAnios);

  const porcentaje =
    antiguedad === null || vidaUtil === null || vidaUtil <= 0 ? null : Math.min(100, Math.max(0, (antiguedad / vidaUtil) * 100));

  let mensaje: string | null = null;
  if (restante !== null) {
    mensaje =
      restante > 0
        ? `Le quedan ${formatoAnios(restante)} años de vida útil`
        : restante === 0
          ? 'Cumple hoy su vida útil'
          : `Cumplió su vida útil hace ${formatoAnios(-restante)} años`;
  }

  return {
    antiguedad: formatoAnios(antiguedad),
    vidaUtil,
    porcentaje,
    mensaje,
    fechaRenovacion: fechaRenovacion(ficha.adquisicion.fechaAdquisicion, ficha.adquisicion.vidaUtilAnios),
  };
}

/** Línea del establecimiento: "Categoría I-4 · Microred Huanta · Huanta, Huanta · Red de Salud Huanta". */
export function detalleEstablecimiento(ficha: FichaActivoDto): string {
  const e = ficha.establecimiento;
  const lugar = [e.distrito, e.provincia].filter((x) => !!x).join(', ');
  return unir(e.categoria ? `Categoría ${e.categoria}` : null, e.microred, lugar, ficha.sedeNombre);
}
