import { ConsultaDniUsuarioDto } from '../../../api/models/consulta-dni-usuario-dto';
import { estadoDniUsuario, mensajeDniUsuario, nombrePropio } from './dni-usuario';

const consulta = (cambios: Partial<ConsultaDniUsuarioDto>): ConsultaDniUsuarioDto => ({
  dni: '41234567',
  reniec: null,
  nombres: null,
  apellidos: null,
  registrado: null,
  ...cambios,
});

describe('nombrePropio', () => {
  it('pasa a nombre propio lo que da RENIEC', () => {
    expect(nombrePropio('MARÍA ELENA')).toBe('María Elena');
    expect(nombrePropio('  QUISPE   HUAMÁN ')).toBe('Quispe Huamán');
  });

  it('deja en minúscula las partículas, salvo al inicio', () => {
    expect(nombrePropio('MARÍA DE LOS ÁNGELES')).toBe('María de los Ángeles');
    expect(nombrePropio('DE LA CRUZ QUISPE')).toBe('De la Cruz Quispe');
  });

  it('pone mayúscula después de guion y apóstrofo', () => {
    expect(nombrePropio("D'ANGELO PÉREZ-ROJAS")).toBe("D'Angelo Pérez-Rojas");
    expect(nombrePropio('ÑAUPARI')).toBe('Ñaupari');
  });
});

describe('estadoDniUsuario', () => {
  it('avisa primero si el DNI ya es de otro usuario', () => {
    expect(estadoDniUsuario(consulta({ registrado: { nombreCompleto: 'Rosa Pérez', sede: 'Red de Salud Puquio' } }))).toBe('registrado');
  });

  it('traduce la respuesta de RENIEC', () => {
    expect(estadoDniUsuario(consulta({ reniec: 'Encontrado' }))).toBe('encontrado');
    expect(estadoDniUsuario(consulta({ reniec: 'NoEncontrado' }))).toBe('noEncontrado');
    expect(estadoDniUsuario(consulta({ reniec: 'NoDisponible' }))).toBe('noDisponible');
  });

  it('pide escribir los nombres cuando RENIEC no los da', () => {
    expect(mensajeDniUsuario('noEncontrado')).toContain('escriba los nombres');
    expect(mensajeDniUsuario('noDisponible')).toContain('Escriba los nombres');
    expect(mensajeDniUsuario('pendiente')).toBe('');
  });
});
