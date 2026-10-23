import { Routes } from '@angular/router';

export const USUARIOS_RUTAS: Routes = [
  { path: '', loadComponent: () => import('./pages/usuarios.page').then((m) => m.UsuariosPage) },
];
