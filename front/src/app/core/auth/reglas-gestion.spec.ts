import { RolDto } from '../../api/models/rol-dto';
import { Permisos } from './permisos';
import { motivoNoGestionable, puedeOtorgarRol } from './reglas-gestion';

const rol = (id: string, permisos: string[], esDelSistema = false): RolDto => ({
  id,
  nombre: id,
  descripcion: null,
  esDelSistema,
  permisos,
  cantidadUsuarios: 0,
});

const editor = rol('editor', [Permisos.Activos.Ver, Permisos.Activos.Crear]);
const consulta = rol('consulta', [Permisos.Activos.Ver]);
const administrador = rol('admin', [Permisos.Usuarios.Gestionar], true);
const gestor = new Set<string>([Permisos.Activos.Ver, Permisos.Usuarios.Gestionar]);

describe('reglas de gestión en el frontend', () => {
  it('solo se otorgan roles cuyos permisos se tienen', () => {
    expect(puedeOtorgarRol(consulta, gestor, false)).toBe(true);
    expect(puedeOtorgarRol(editor, gestor, false)).toBe(false);
  });

  it('el rol Administrador de sede solo lo otorga el superadministrador', () => {
    expect(puedeOtorgarRol(administrador, new Set([Permisos.Usuarios.Gestionar]), false)).toBe(false);
    expect(puedeOtorgarRol(administrador, new Set(), true)).toBe(true);
  });

  it('nadie gestiona a quien tiene más permisos, ni a un administrador, ni a sí mismo', () => {
    const actual = { usuarioId: 'yo', esSuperAdmin: false, permisos: gestor };
    const roles = [editor, consulta, administrador];

    expect(motivoNoGestionable({ id: 'a', roles: [{ id: 'consulta' }] }, roles, actual)).toBeNull();
    expect(motivoNoGestionable({ id: 'b', roles: [{ id: 'editor' }] }, roles, actual)).toContain('permisos');
    expect(motivoNoGestionable({ id: 'c', roles: [{ id: 'admin' }] }, roles, actual)).toContain('superadministrador');
    expect(motivoNoGestionable({ id: 'yo', roles: [] }, roles, actual)).toContain('propia');
  });
});
