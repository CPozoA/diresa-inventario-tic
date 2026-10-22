# Arquitectura del Sistema de Inventario TIC

> Documento vivo. Registra **qué** decidimos y **por qué**. Cualquier cambio de arquitectura se anota aquí antes de programarlo.
>
> Versión 0.3 · 22/10/2026 · Cambios: D16 (varios roles por usuario), D17 (bloqueo tras 3 intentos), D18 (sedes iniciales).

---

## 1. Propósito y alcance

Sistema web para que **7 sedes** registren y gestionen su inventario de Tecnologías de la Información y Comunicación (TIC): activos, sus componentes (discos, memoria RAM), licencias de software, mantenimientos y asignaciones.

Reglas de negocio que dan forma a la arquitectura:

- Cada sede **solo ve y gestiona su propia información**.
- Cada sede **gestiona sus usuarios y sus roles**.
- Existe un **superadministrador** que ve y gestiona todo.
- Un usuario pertenece a **una sola sede**.
- **No existen transferencias** de activos entre sedes: un activo nunca cambia de sede.

### 1.1 Sedes

| Código | Nombre |
|--------|--------|
| `HUANTA` | Red de Salud Huanta |
| `HUAMANGA` | Red de Salud Huamanga |
| `CENTRO` | Red de Salud Centro |
| `SAN-FRANCISCO` | Red de Salud San Francisco |
| `SAN-MIGUEL` | Red de Salud San Miguel |
| `PUQUIO` | Red de Salud Puquio |
| `CORACORA` | Red de Salud Coracora |

El superadministrador no pertenece a ninguna sede. Se pueden agregar sedes nuevas desde la aplicación (solo el superadmin).

---

## 2. Decisiones tomadas

| # | Tema | Decisión | Motivo |
|---|------|----------|--------|
| D1 | Backend | **.NET 10** (ASP.NET Core) | Versión LTS actual; soportada por el hosting elegido. |
| D2 | Frontend | **Angular 22** (última estable) + **Angular Material** | Pedido del proyecto; Material da componentes accesibles y probados. |
| D3 | Base de datos | **SQL Server** con **Entity Framework Core** | Disponible en el hosting; EF Core da migraciones versionadas. |
| D4 | Arquitectura backend | **Clean Architecture organizada por funcionalidades** + **CQRS ligero** | Responsabilidades separadas, reglas de negocio aisladas de la infraestructura, código fácil de encontrar. |
| D5 | Arquitectura frontend | **Organización por funcionalidades** + **componentes standalone** + **NgRx SignalStore** | Misma lógica que el backend; estado predecible con signals. |
| D6 | Autenticación | **Usuario y contraseña propios** con **ASP.NET Core Identity** y **cookie** HttpOnly | La app sirve el frontend desde el mismo dominio: la cookie es la opción más segura (no hay tokens en el navegador). |
| D7 | Autorización | **Por permisos**; los roles son conjuntos de permisos; **roles configurables por sede** | Permite que cada sede defina sus roles sin tocar código. |
| D8 | Separación por sede | **Columna `SedeId`** en cada tabla de la sede + **filtro global** de EF Core + asignación automática al guardar | Una sola base de datos; imposible olvidar el filtro en un endpoint nuevo. |
| D9 | Catálogos | **Mixtos**: globales (los administra el superadmin) y por sede | Reportes consolidados coherentes entre sedes, sin quitar autonomía local. |
| D10 | Código patrimonial | **Único dentro de cada sede** (no global) | Cada sede ya tiene su propia numeración. |
| D11 | Recuperación de contraseña | **Ambas vías**: enlace por correo (SMTP) y restablecimiento por el administrador | Pedido del proyecto. |
| D12 | Hosting | **SmarterASP.NET** (IIS) ahora; luego **servidor propio con Proxmox** | Toda la configuración por archivos y variables de entorno para que la migración sea solo despliegue. |
| D13 | Mediador | **Sin MediatR** (licencia comercial desde 2025): interfaces de handler propias y registro en DI | Misma claridad, sin dependencia de pago. |
| D14 | Mapeo de objetos | **Mapeo manual** (o Mapperly si crece) | Sin AutoMapper (licencia comercial); el mapeo explícito es más fácil de depurar. |
| D15 | Estilo de API | **Minimal APIs agrupadas por funcionalidad** | Encaja con la organización por funcionalidades; menos ceremonia que los controladores. |
| D16 | Roles por usuario | **Varios roles por usuario**; sus permisos son la **suma** de los de todos sus roles | En las sedes una persona suele cumplir varias funciones; evita crear roles combinados y que un cambio en un rol llegue a todos sus usuarios. |
| D17 | Valores de seguridad | Contraseña de mínimo **10 caracteres**; **bloqueo de 15 min tras 3 intentos fallidos**; sesión que vence a los **30 min sin actividad** | Definidos por el responsable del proyecto. |
| D18 | Sedes iniciales | Las **7 redes de salud** listadas en §1.1 | Datos iniciales del sistema. |

### Decisiones pendientes de confirmar

| # | Tema | Propuesta |
|---|------|-----------|
| P2 | Datos del servidor SMTP | Se configuran en el despliegue (servidor, puerto, usuario, remitente). |

---

## 3. Visión general

```mermaid
flowchart LR
  subgraph Navegador
    A[Angular 22<br/>Angular Material]
  end
  subgraph Servidor["Servidor (IIS / Docker)"]
    B[Inventario.Api<br/>ASP.NET Core .NET 10]
    C[(SQL Server)]
    D[[Servidor SMTP]]
  end
  A -- "HTTPS + cookie de sesión" --> B
  B -- EF Core --> C
  B -- correo de recuperación --> D
```

El frontend compilado se publica **dentro** de la API (carpeta `wwwroot`). Un solo dominio y un solo despliegue: sin CORS y con la cookie protegida por `SameSite=Strict`.

---

## 4. Estructura del repositorio

```
Inventario TIC/
├─ docs/
│  └─ ARQUITECTURA.md          ← este documento
├─ back/                        ← solución .NET 10
│  ├─ src/
│  │  ├─ Inventario.Domain/
│  │  ├─ Inventario.Application/
│  │  ├─ Inventario.Infrastructure/
│  │  └─ Inventario.Api/
│  └─ tests/
│     ├─ Inventario.Domain.Tests/
│     ├─ Inventario.Application.Tests/
│     ├─ Inventario.Api.IntegrationTests/
│     └─ Inventario.ArchitectureTests/
└─ front/                       ← aplicación Angular 22
   └─ src/app/
      ├─ core/
      ├─ shared/
      ├─ features/
      └─ api/
```

---

## 5. Backend

### 5.1 Capas y dependencias

```mermaid
flowchart TB
  Api[Inventario.Api] --> Application
  Api --> Infrastructure
  Infrastructure --> Application
  Application --> Domain[Inventario.Domain]
```

| Capa | Contiene | No puede |
|------|----------|----------|
| **Domain** | Entidades (`Activo`, `Disco`, `Sede`…), objetos de valor, reglas puras, excepciones de dominio. | Depender de ninguna otra capa ni de EF Core, ASP.NET o librerías externas. |
| **Application** | Casos de uso por funcionalidad: comandos, consultas, handlers, validadores, DTOs, interfaces (`IAppDbContext`, `IUsuarioActual`, `IEmailSender`). | Conocer SQL Server, HTTP ni detalles de Identity. |
| **Infrastructure** | `AppDbContext`, configuraciones de EF Core, migraciones, Identity, envío de correo, interceptores. | Contener reglas de negocio. |
| **Api** | Endpoints (Minimal APIs), autenticación, autorización, manejo global de errores, OpenAPI, archivos del frontend. | Contener lógica de negocio: solo recibe, delega y responde. |

Estas reglas se **verifican automáticamente** con pruebas de arquitectura (`Inventario.ArchitectureTests`).

### 5.2 Organización por funcionalidades

Dentro de `Application` (y de los endpoints en `Api`), cada funcionalidad tiene su carpeta:

```
Inventario.Application/
├─ Activos/
│  ├─ Crear/
│  │  ├─ CrearActivoCommand.cs
│  │  ├─ CrearActivoHandler.cs
│  │  └─ CrearActivoValidator.cs
│  ├─ Editar/
│  ├─ ObtenerFicha/
│  │  ├─ ObtenerFichaQuery.cs
│  │  ├─ ObtenerFichaHandler.cs
│  │  └─ FichaActivoDto.cs
│  └─ Listar/
├─ Sedes/
├─ Usuarios/
├─ Roles/
├─ Catalogos/
└─ Common/            ← interfaces y piezas compartidas
```

### 5.3 CQRS ligero

- **Comando**: cambia datos (`CrearActivo`, `RestablecerContraseña`). Devuelve un `Result`.
- **Consulta**: solo lee (`ListarActivos`, `ObtenerFicha`). Usa `AsNoTracking` y proyecta directo a DTO.
- Interfaces propias:

```csharp
public interface ICommandHandler<TCommand, TResult> { Task<Result<TResult>> Handle(TCommand command, CancellationToken ct); }
public interface IQueryHandler<TQuery, TResult>   { Task<Result<TResult>> Handle(TQuery query, CancellationToken ct); }
```

- La validación (FluentValidation) se ejecuta antes del handler mediante un **decorador** registrado en DI.
- Misma base de datos para lectura y escritura: es una separación **de código**, no de infraestructura.

### 5.4 Acceso a datos

- **Sin repositorio genérico.** EF Core ya implementa los patrones Repository y Unit of Work. Los handlers usan `IAppDbContext` (interfaz definida en Application, implementada en Infrastructure).
- Configuración de cada entidad en su propia clase `IEntityTypeConfiguration<T>`.
- Migraciones versionadas en `Infrastructure/Persistence/Migrations`.

### 5.5 Errores

- **Errores esperados** (reglas de negocio, validación, no encontrado, sin permiso): se devuelven como `Result` con un código de error y se traducen a respuestas HTTP con formato estándar **ProblemDetails** (RFC 9457).
- **Errores inesperados**: los captura un manejador global, se registran en el log con un identificador y el usuario ve un mensaje genérico con ese identificador (nunca detalles internos).

### 5.6 Separación por sede (multi-sede)

Es la pieza de seguridad más importante del sistema.

1. **`IUsuarioActual`** expone `UsuarioId`, `SedeId` (nulo para el superadmin), `EsSuperAdmin` y los permisos. Se lee **siempre de la sesión** del servidor, nunca de datos enviados por el navegador.
2. Toda entidad de una sede implementa `IPerteneceASede { Guid SedeId }`.
3. **Filtro global de EF Core** sobre cada `IPerteneceASede`: `e => usuarioActual.EsSuperAdmin || e.SedeId == usuarioActual.SedeId`.
4. **Interceptor de guardado** (`SaveChangesInterceptor`):
   - Al crear: asigna `SedeId` desde la sesión. Solo el superadmin puede indicar la sede explícitamente.
   - Al modificar: **impide cambiar** `SedeId` (no hay transferencias).
   - Rellena los campos de auditoría (`CreadoPor`, `CreadoEn`, `ModificadoPor`, `ModificadoEn`).
5. Cualquier excepción al filtro (`IgnoreQueryFilters`) solo se permite en código revisado y marcado.
6. **Pruebas de aislamiento** obligatorias (ver §8).

### 5.7 Catálogos

| Globales (superadmin) | Por sede (administrador de sede) |
|---|---|
| Tipos de activo, marcas, sistemas operativos, tipos de licencia, tipos de mantenimiento, estados, tipos de almacenamiento | Áreas o dependencias, ubicaciones físicas |

Los catálogos por sede usan el mismo mecanismo de §5.6. Los catálogos no se borran si están en uso: se **desactivan**.

---

## 6. Seguridad

### 6.1 Autenticación

- **ASP.NET Core Identity** con usuario y contraseña propios. Las contraseñas se guardan con hash PBKDF2 (nunca en texto plano).
- **Cookie de sesión**: `HttpOnly`, `Secure`, `SameSite=Strict`, vencimiento por inactividad (propuesta: 30 minutos, renovable mientras haya actividad).
- **Protección CSRF**: token antifalsificación en cada petición que modifica datos.
- **Política de contraseñas**: mínimo 10 caracteres, con mayúscula, minúscula y número.
- **Bloqueo** tras **3 intentos fallidos** durante 15 minutos (D17). El desbloqueo es automático al vencer el plazo, y el administrador de sede (o el superadmin) puede desbloquear antes. Como un umbral bajo facilita que alguien bloquee cuentas ajenas a propósito, el inicio de sesión tiene además límite de peticiones por dirección IP (§6.4) y los bloqueos quedan registrados.
- **Cuentas nuevas y restablecidas** llevan contraseña temporal y **cambio obligatorio** en el primer ingreso.
- **Recuperación de contraseña**:
  - *Por correo*: enlace de un solo uso que vence en 1 hora. La respuesta es la misma exista o no el correo, para no revelar qué cuentas existen.
  - *Por el administrador*: genera una contraseña temporal; el usuario debe cambiarla al entrar.
- Las **claves de protección de datos** de ASP.NET (que cifran la cookie) se guardan en la base de datos, para que las sesiones no se pierdan cuando el hosting recicla la aplicación.

### 6.2 Autorización por permisos

- Los **permisos** están definidos en el código (no se inventan desde la interfaz):
  `Activos.Ver`, `Activos.Crear`, `Activos.Editar`, `Activos.Eliminar`, `Licencias.Ver`, `Licencias.Gestionar`, `Mantenimiento.Ver`, `Mantenimiento.Gestionar`, `Asignaciones.Ver`, `Asignaciones.Gestionar`, `Catalogos.GestionarSede`, `Usuarios.Ver`, `Usuarios.Gestionar`, `Roles.Gestionar`, y los exclusivos del superadmin: `Sedes.Gestionar`, `Catalogos.GestionarGlobales`, `Administradores.Gestionar`.
- Un usuario puede tener **varios roles** (D16). Sus permisos efectivos son la **unión** de los permisos de todos sus roles. La pantalla del usuario muestra ese resumen para que se entienda qué puede hacer sin sumar roles a mano.
- Cada endpoint declara el permiso que exige (`.RequirePermiso(Permisos.Activos.Crear)`).
- Los permisos del usuario se resuelven en el servidor en cada petición (con caché corta que se invalida al cambiar un rol). Así, quitar un permiso tiene efecto inmediato.
- Los roles **no** usan la tabla de roles de Identity (exige nombres únicos globales y "Editor" existirá en cada sede); se usan tablas propias.

### 6.3 Roles

| Rol | Tipo | Quién lo asigna | Alcance |
|-----|------|-----------------|---------|
| **Superadmin** | Sistema | — | Todas las sedes y todos los permisos. |
| **Administrador de sede** | Sistema | Solo el superadmin | Todo dentro de su sede, incluidos usuarios y roles. |
| **Editor** | Inicial de cada sede (editable) | Administrador de sede | Registrar y modificar inventario. |
| **Consulta** | Inicial de cada sede (editable) | Administrador de sede | Solo lectura. |
| *Roles propios* | Por sede | Administrador de sede | Lo que se marque. |

**Reglas contra el escalamiento de privilegios** (validadas en el servidor):

1. Un administrador de sede solo puede otorgar permisos **que él mismo tiene**, tanto al armar un rol como al asignar roles a un usuario.
2. Los permisos exclusivos del superadmin **nunca** se pueden asignar a un rol de sede.
3. Nadie de una sede puede crear, editar ni asignar el rol **Administrador de sede**.
4. Un usuario solo puede recibir roles **de su propia sede** (o roles del sistema, si los asigna el superadmin).
5. Los roles de una sede son **invisibles** para las demás.
6. Nadie puede modificar sus propios roles.
7. El rol **Superadmin** no se combina con roles de sede.

### 6.4 Otras medidas

- Cabeceras de seguridad: HSTS, `Content-Security-Policy`, `X-Content-Type-Options`, `Referrer-Policy`.
- Límite de peticiones en inicio de sesión y recuperación de contraseña.
- Cadenas de conexión y credenciales SMTP **fuera del repositorio** (variables de entorno o `appsettings.Production.json` no versionado).
- Registro de auditoría de cambios (fase de endurecimiento).

---

## 7. Frontend

### 7.1 Estructura

```
front/src/app/
├─ core/                      ← se carga una sola vez
│  ├─ auth/                   ← sesión, guard de permisos, servicio de usuario actual
│  ├─ http/                   ← interceptores (errores, CSRF, sesión vencida)
│  └─ layout/                 ← barra lateral, encabezado, selector de sede (superadmin)
├─ shared/                    ← sin lógica de negocio
│  ├─ ui/                     ← insignia de estado, tabla, tarjeta de indicador, confirmación
│  └─ pipes/                  ← fecha dd/mm/aaaa, moneda S/
├─ features/                  ← una carpeta por funcionalidad, carga diferida
│  ├─ resumen/
│  ├─ activos/
│  │  ├─ pages/               ← componentes contenedores (rutas)
│  │  ├─ components/          ← componentes de presentación
│  │  ├─ data/                ← store (SignalStore) y acceso a la API
│  │  └─ activos.routes.ts
│  ├─ licencias/
│  ├─ sedes/
│  ├─ usuarios/
│  ├─ roles/
│  └─ catalogos/
└─ api/                       ← cliente generado desde OpenAPI (no se edita a mano)
```

### 7.2 Patrones

- **Componentes standalone** y **rutas con carga diferida** por funcionalidad.
- **Contenedor / presentación**: la página obtiene datos y coordina; los componentes visuales solo reciben `input()` y emiten `output()`.
- **Estado con NgRx SignalStore**: un store por funcionalidad, con la misma forma en todas (estado, selectores computados, métodos).
- **Cliente de API generado** desde el contrato OpenAPI del backend: si el backend cambia, el frontend no compila hasta actualizarse.
- **Formularios reactivos tipados**; listas dinámicas para discos y módulos de RAM.
- **Guards por permiso** para rutas y una directiva para mostrar u ocultar botones. El servidor siempre tiene la última palabra.
- **Detección de cambios basada en signals** (`OnPush` en todos los componentes).
- Formato regional **es-PE**: fechas `dd/mm/aaaa`, moneda `S/`.

### 7.3 Diseño visual

Angular Material, con un tema propio basado en los bosquejos (barra lateral oscura, acento azul para acciones, verde/ámbar/rojo para estados, códigos en tipografía monoespaciada).

---

## 8. Pruebas

| Proyecto | Qué prueba |
|----------|-----------|
| `Domain.Tests` | Reglas puras (vida útil, garantía, suma de capacidades). |
| `Application.Tests` | Handlers y validadores. |
| `Api.IntegrationTests` | Endpoints reales contra SQL Server de pruebas. |
| `ArchitectureTests` | Que las capas respeten sus dependencias. |
| Frontend | Pruebas unitarias de stores y componentes; pruebas de punta a punta de los flujos principales. |

**Pruebas de aislamiento entre sedes (obligatorias)**. Con dos sedes A y B, un usuario de A:

- no ve activos, usuarios, roles ni catálogos de B en ningún listado;
- recibe "no encontrado" al pedir por identificador un registro de B;
- no puede editar ni eliminar registros de B;
- no puede crear registros en B indicando su `SedeId`;
- no puede asignarse ni asignar a otros permisos que no tiene;
- no puede asignar a un usuario de A un rol que pertenece a B.

---

## 9. Convenciones

- **Idioma**: conceptos de negocio en español (`Activo`, `Sede`, `CrearActivo`); sufijos técnicos en inglés por convención (`Command`, `Handler`, `Validator`, `Store`).
- **.NET**: `Nullable` activado, advertencias tratadas como errores, analizadores de código, `async` en todo acceso a datos con `CancellationToken`.
- **Angular**: TypeScript estricto, ESLint y Prettier.
- **Commits** con formato convencional (`feat:`, `fix:`, `docs:`…).
- Archivo `.editorconfig` compartido.

---

## 10. Despliegue

| Entorno | Cómo |
|---------|------|
| Desarrollo | `dotnet run` (API) + `ng serve` (Angular con proxy a la API); SQL Server local o LocalDB. |
| SmarterASP.NET | Publicación de la API con el frontend compilado en `wwwroot`; IIS en proceso; base SQL Server del hosting. |
| Proxmox (futuro) | Contenedor Docker o máquina virtual con IIS; misma configuración por variables de entorno. |

Consideraciones del hosting compartido:

- IIS recicla la aplicación: **no** se usan tareas en segundo plano permanentes.
- Las claves de protección de datos se guardan en la base de datos (§6.1).
- Las migraciones se aplican con un script SQL generado (`dotnet ef migrations script --idempotent`), no automáticamente al arrancar.

---

## 11. Plan por pasos

| Paso | Contenido | Verificación |
|------|-----------|--------------|
| **0** | Este documento y la estructura de carpetas | Revisión |
| **1** | Solución .NET con las 4 capas y entidades base (sede, usuario, rol, permiso) | `dotnet build` |
| **2** | Base de datos y primera migración | Migración aplicada en SQL Server |
| **3** | Inicio de sesión, contraseñas, bloqueo y recuperación | Prueba manual (Postman o navegador) |
| **4** | Permisos, roles por sede y aislamiento, con pruebas automáticas | `dotnet test` |
| **5** | Angular 22 + Material: diseño base e inicio de sesión | `ng serve` |
| **6** | Pantallas de sedes, usuarios, roles y catálogos | Prueba manual |
| **7** | Módulo de activos básico (con discos y RAM) | Prueba de punta a punta |

Fases posteriores: licencias, mantenimiento, asignaciones, resumen, exportación a Excel, registro de auditoría y endurecimiento.
