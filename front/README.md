# Frontend: Inventario TIC (Angular 22)

Angular 22 con Angular Material, componentes standalone, signals y NgRx SignalStore.
Arquitectura y decisiones: [docs/ARQUITECTURA.md §7](../docs/ARQUITECTURA.md#7-frontend).

## Requisitos

- **Node.js 24 LTS** (mínimo 22.22.3 o 24.15). Compruébelo con `node -v`.
- La API corriendo en `https://localhost:7180` (carpeta `back`: `dotnet run --project src/Inventario.Api`).

## Primera vez

Desde la carpeta `front`:

```powershell
# 1. Instala las dependencias (exactas, según package-lock.json)
npm ci

# 2. Exporta el certificado HTTPS de desarrollo de .NET (el mismo de la API) a front/.certs/
#    para que ng serve funcione en https://localhost:4200 sin advertencias. Una sola vez por PC.
npm run certificado
```

## Día a día

```powershell
npm start          # https://localhost:4200 (las llamadas a /api se reenvían a la API)
npm test           # pruebas unitarias (Vitest)
npm run build      # compilación de producción en dist/
```

## Cliente de la API (generado)

`src/app/api/` se **genera** a partir del contrato OpenAPI de la API: no se edita a mano.
Cuando cambie la API, con la API corriendo:

```powershell
npm run api:actualizar   # descarga openapi/inventario-api.json y regenera src/app/api/
```

Si después de regenerar el frontend no compila, es porque la API cambió algo que el frontend usa: el error de
compilación indica qué ajustar. Ambos (el contrato y el código generado) se suben al repositorio, así los cambios
de la API se ven en cada commit.

## Estructura

```
src/app/
├─ api/        cliente generado (ng-openapi-gen); no se edita
├─ core/       se carga una sola vez
│  ├─ auth/    SesionStore (sesión y permisos), guards, directiva *siTienePermiso, códigos de permiso
│  └─ http/    token antifalsificación (CSRF), manejo común de errores (401, contraseña temporal)
├─ shared/     componentes y pipes sin lógica de negocio (Paso 5B en adelante)
└─ features/   una carpeta por funcionalidad, con carga diferida
```

## Cómo se conecta con la API

- En desarrollo, `ng serve` reenvía `/api` a `https://localhost:7180` (`proxy.conf.json`). El navegador ve un solo
  sitio, así que la cookie de sesión (`HttpOnly`, `SameSite=Strict`) funciona sin CORS.
- En producción, la API publica el frontend compilado en su carpeta `wwwroot` (mismo sitio, sin proxy).
- Toda petición que cambia datos lleva la cabecera `X-XSRF-TOKEN` (protección CSRF). El token se pide a
  `GET /api/cuenta/csrf` y se renueva al iniciar o cerrar sesión (`core/http/csrf.interceptor.ts`).
- Las fuentes (IBM Plex) y los íconos (Material Icons) se sirven desde la propia aplicación: no depende de
  Google Fonts ni de internet en la red de la sede.

Bosquejos de referencia: los PNG en la carpeta raíz del proyecto.
