import { CodigoPermiso, Permisos } from '../auth/permisos';

/** Una opción del menú lateral. Sin permiso, la ve cualquier usuario con sesión. */
export interface OpcionMenu {
  readonly texto: string;
  readonly ruta: string;
  readonly permiso?: CodigoPermiso;
}

export interface GrupoMenu {
  readonly titulo: string;
  readonly opciones: readonly OpcionMenu[];
}

/**
 * Menú de la aplicación. Es la única definición: de aquí salen el menú lateral y las rutas de cada funcionalidad
 * (con su guard de permiso). Al agregar una funcionalidad nueva, se agrega aquí.
 */
export const MENU: readonly GrupoMenu[] = [
  {
    titulo: 'Inventario',
    opciones: [
      { texto: 'Resumen', ruta: '' },
      { texto: 'Inventario', ruta: 'inventario', permiso: Permisos.Activos.Ver },
      { texto: 'Licencias', ruta: 'licencias', permiso: Permisos.Licencias.Ver },
      { texto: 'Mantenimiento', ruta: 'mantenimiento', permiso: Permisos.Mantenimiento.Ver },
      { texto: 'Asignaciones', ruta: 'asignaciones', permiso: Permisos.Asignaciones.Ver },
    ],
  },
  {
    titulo: 'Administración',
    opciones: [
      { texto: 'Usuarios', ruta: 'usuarios', permiso: Permisos.Usuarios.Ver },
      { texto: 'Roles', ruta: 'roles', permiso: Permisos.Roles.Ver },
      { texto: 'Áreas', ruta: 'areas', permiso: Permisos.Catalogos.GestionarSede },
      { texto: 'Sedes', ruta: 'sedes', permiso: Permisos.Sedes.Gestionar },
      { texto: 'Establecimientos', ruta: 'establecimientos', permiso: Permisos.Establecimientos.Gestionar },
      { texto: 'Catálogos generales', ruta: 'catalogos', permiso: Permisos.Catalogos.GestionarGlobales },
    ],
  },
];

/** Menú filtrado según los permisos del usuario; los grupos sin opciones visibles desaparecen. */
export function menuVisible(permisos: ReadonlySet<string>): GrupoMenu[] {
  return MENU.map((grupo) => ({
    titulo: grupo.titulo,
    opciones: grupo.opciones.filter((o) => o.permiso === undefined || permisos.has(o.permiso)),
  })).filter((grupo) => grupo.opciones.length > 0);
}
