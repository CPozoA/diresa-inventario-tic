import { Permisos } from '../auth/permisos';
import { MENU, menuVisible } from './menu';

const todos = new Set(MENU.flatMap((g) => g.opciones.map((o) => o.permiso)).filter((p): p is NonNullable<typeof p> => !!p));

describe('menuVisible', () => {
  it('el superadministrador ve todas las opciones', () => {
    const opciones = menuVisible(todos).flatMap((g) => g.opciones.map((o) => o.texto));

    expect(opciones).toContain('Sedes');
    expect(opciones).toContain('Establecimientos');
    expect(opciones).toContain('Catálogos generales');
    expect(opciones).toContain('Auditoría');
  });

  it('un usuario de consulta no ve el grupo de administración', () => {
    const grupos = menuVisible(new Set([Permisos.Activos.Ver, Permisos.Licencias.Ver]));

    expect(grupos.map((g) => g.titulo)).toEqual(['Inventario']);
    expect(grupos[0].opciones.map((o) => o.texto)).toEqual(['Resumen', 'Inventario', 'Licencias', 'Asignaciones']);
  });

  it('el administrador de sede ve usuarios y roles, pero no sedes', () => {
    const opciones = menuVisible(new Set([Permisos.Usuarios.Ver, Permisos.Roles.Ver])).flatMap((g) => g.opciones.map((o) => o.texto));

    expect(opciones).toContain('Usuarios');
    expect(opciones).not.toContain('Sedes');
    expect(opciones).not.toContain('Establecimientos');
  });
});
