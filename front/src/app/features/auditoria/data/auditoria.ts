import { ModuloAuditoria } from '../../../api/models/modulo-auditoria';

export const TEXTO_MODULO: Readonly<Record<NonNullable<ModuloAuditoria>, string>> = {
  Activos: 'Activos',
  Asignaciones: 'Asignaciones',
  Personal: 'Personal',
  Mantenimiento: 'Mantenimiento',
  Licencias: 'Licencias',
  Catalogos: 'Catálogos',
  Establecimientos: 'Establecimientos',
  Usuarios: 'Usuarios',
  Roles: 'Roles',
  Sedes: 'Sedes',
  Cuenta: 'Cuenta y sesiones',
};

export const MODULOS = Object.keys(TEXTO_MODULO) as readonly NonNullable<ModuloAuditoria>[];

export function textoModulo(modulo: ModuloAuditoria | null | undefined): string {
  return modulo ? TEXTO_MODULO[modulo] : '';
}

/** Intentos fallidos y bloqueos se resaltan: son lo que más interesa revisar. */
export function esAlerta(accion: string): boolean {
  return /^(Inicio de sesión fallido|Intento de inicio de sesión)/.test(accion);
}

/** "30/10/2026 14:05" en la hora local del navegador. */
export function fechaHora(valor: string): string {
  const fecha = new Date(valor);
  if (Number.isNaN(fecha.getTime())) {
    return '—';
  }
  const dos = (n: number) => String(n).padStart(2, '0');
  return `${dos(fecha.getDate())}/${dos(fecha.getMonth() + 1)}/${fecha.getFullYear()} ${dos(fecha.getHours())}:${dos(fecha.getMinutes())}`;
}

// ---------- Filtros (quedan en la dirección) ----------

export interface FiltrosAuditoria {
  readonly desde: string;
  readonly hasta: string;
  readonly modulo: NonNullable<ModuloAuditoria> | null;
  readonly buscar: string;
  readonly pagina: number;
}

export const FILTROS_AUDITORIA_INICIALES: FiltrosAuditoria = { desde: '', hasta: '', modulo: null, buscar: '', pagina: 1 };

const FECHA = /^\d{4}-\d{2}-\d{2}$/;

export function filtrosAuditoriaDesdeParametros(p: Readonly<Record<string, string | undefined>>): FiltrosAuditoria {
  const modulo = p['modulo'];
  const pagina = Number(p['pagina']);
  return {
    desde: FECHA.test(p['desde'] ?? '') ? p['desde']! : '',
    hasta: FECHA.test(p['hasta'] ?? '') ? p['hasta']! : '',
    modulo: modulo && (MODULOS as readonly string[]).includes(modulo) ? (modulo as NonNullable<ModuloAuditoria>) : null,
    buscar: (p['buscar'] ?? '').trim(),
    pagina: Number.isInteger(pagina) && pagina > 1 ? pagina : 1,
  };
}

export function parametrosDesdeFiltrosAuditoria(f: FiltrosAuditoria): Record<string, string> {
  const p: Record<string, string> = {};
  if (f.desde) p['desde'] = f.desde;
  if (f.hasta) p['hasta'] = f.hasta;
  if (f.modulo) p['modulo'] = f.modulo;
  if (f.buscar.trim()) p['buscar'] = f.buscar.trim();
  if (f.pagina > 1) p['pagina'] = String(f.pagina);
  return p;
}

export function hayFiltrosAuditoria(f: FiltrosAuditoria): boolean {
  return f.desde !== '' || f.hasta !== '' || f.modulo !== null || f.buscar !== '';
}
