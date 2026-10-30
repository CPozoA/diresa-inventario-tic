/**
 * Códigos de permiso. Son los mismos de la API (back/src/Inventario.Domain/Seguridad/Permisos.cs).
 * El frontend los usa solo para mostrar u ocultar opciones: la API siempre vuelve a validar.
 */
export const Permisos = {
  Activos: { Ver: 'Activos.Ver', Crear: 'Activos.Crear', Editar: 'Activos.Editar', Eliminar: 'Activos.Eliminar' },
  Licencias: { Ver: 'Licencias.Ver', Gestionar: 'Licencias.Gestionar' },
  Mantenimiento: { Ver: 'Mantenimiento.Ver', Gestionar: 'Mantenimiento.Gestionar' },
  Asignaciones: { Ver: 'Asignaciones.Ver', Gestionar: 'Asignaciones.Gestionar' },
  Catalogos: { GestionarSede: 'Catalogos.GestionarSede', GestionarGlobales: 'Catalogos.GestionarGlobales' },
  Usuarios: { Ver: 'Usuarios.Ver', Gestionar: 'Usuarios.Gestionar' },
  Roles: { Ver: 'Roles.Ver', Gestionar: 'Roles.Gestionar' },
  Auditoria: { Ver: 'Auditoria.Ver' },
  Sedes: { Gestionar: 'Sedes.Gestionar' },
  Establecimientos: { Gestionar: 'Establecimientos.Gestionar' },
  Administradores: { Gestionar: 'Administradores.Gestionar' },
} as const;

type Valores<T> = T extends string ? T : { [K in keyof T]: Valores<T[K]> }[keyof T];

/** Cualquier código de permiso válido. */
export type CodigoPermiso = Valores<typeof Permisos>;
