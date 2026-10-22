# Backend: Inventario TIC (.NET 10)

API en ASP.NET Core con Clean Architecture organizada por funcionalidades.
Detalle y justificación: [docs/ARQUITECTURA.md §5](../docs/ARQUITECTURA.md#5-backend).

## Estructura

```
back/
├─ Inventario.slnx               solución
├─ global.json                   fija el SDK .NET 10 (admite parches más nuevos)
├─ Directory.Build.props         configuración común: net10.0, nullable, analizadores, advertencias = errores
├─ Directory.Packages.props      versiones de paquetes NuGet centralizadas
├─ .config/dotnet-tools.json     herramientas locales (dotnet-ef)
└─ src/
   ├─ Inventario.Domain/         entidades y reglas puras, sin dependencias
   │  ├─ Common/                 Entidad, EntidadAuditable, IPerteneceASede, DominioException
   │  ├─ Sedes/                  Sede, SedesIniciales (las 7 redes de salud)
   │  ├─ Seguridad/              Permisos (catálogo), Rol, RolPermiso, TipoRol, RolesDelSistema
   │  └─ Usuarios/               Usuario, UsuarioRol
   ├─ Inventario.Application/    casos de uso por funcionalidad
   │  ├─ Common/                 Result, ErrorAplicacion, ICommand/IQuery, IUsuarioActual, IAppDbContext
   │  └─ Sedes/Listar/           primera consulta de ejemplo (ListarSedes)
   ├─ Inventario.Infrastructure/ persistencia, Identity, correo
   │  ├─ Identidad/              CuentaUsuario (credenciales de Identity)
   │  └─ Persistencia/           AppDbContext, Configuraciones/, Interceptores/, Inicializacion/, Migraciones/
   └─ Inventario.Api/            endpoints (Minimal APIs), seguridad, errores
      ├─ Common/                 traducción de resultados a respuestas HTTP
      ├─ Endpoints/              /api/salud, /api/sedes
      └─ Seguridad/              usuario actual leído de la sesión
```

Los proyectos de pruebas (`tests/`) se agregan en el Paso 4.

## Base de datos

| Esquema | Tablas |
|---------|--------|
| `organizacion` | `Sedes` |
| `seguridad` | `Usuarios`, `Cuentas` (+ `CuentaClaims`, `CuentaLogins`, `CuentaTokens`), `Roles`, `RolPermisos`, `UsuarioRoles`, `ClavesProteccionDatos` |

Al arrancar, la API carga los **datos iniciales** si faltan: las 7 sedes, los roles del sistema
(Superadministrador y Administrador de sede) y los roles Editor y Consulta de cada sede.
No duplica nada si se ejecuta varias veces.

## Primera vez: crear la base de datos

Requisitos: SDK de .NET 10 y SQL Server Express en `.\SQLEXPRESS` con autenticación de Windows.
La cadena de conexión de desarrollo está en `src/Inventario.Api/appsettings.Development.json`.
No lleva contraseña, por eso puede estar en el repositorio.

Desde la carpeta `back`:

```powershell
# 1. Instala la herramienta de migraciones (dotnet-ef) declarada en .config/dotnet-tools.json
dotnet tool restore

# 2. Compila
dotnet build

# 3. Crea la migración inicial (genera archivos en Infrastructure/Persistencia/Migraciones)
dotnet ef migrations add Inicial --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones

# 4. Crea la base de datos InventarioTIC y sus tablas
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api

# 5. Arranca la API (carga los datos iniciales)
dotnet run --project src/Inventario.Api --launch-profile https
```

Luego abre:

- <https://localhost:7180/scalar>: documentación interactiva de la API (solo en desarrollo).
- <https://localhost:7180/api/sedes>: debe devolver las 7 sedes.
- <https://localhost:7180/api/salud>: comprobación rápida.

Si el navegador advierte por el certificado de desarrollo, ejecuta una vez `dotnet dev-certs https --trust`.

## Cambios en el modelo (migraciones siguientes)

Cada vez que cambie una entidad o su configuración:

```powershell
dotnet ef migrations add NombreDelCambio --project src/Inventario.Infrastructure --startup-project src/Inventario.Api --output-dir Persistencia/Migraciones
dotnet ef database update --project src/Inventario.Infrastructure --startup-project src/Inventario.Api
```

Para producción no se aplican migraciones al arrancar: se genera un script revisable con
`dotnet ef migrations script --idempotent ...` (docs/ARQUITECTURA.md §10).

## Decisiones de diseño

- **Usuario y credenciales separados (D19).** `Domain.Usuarios.Usuario` guarda los datos de negocio (sede, nombres, roles).
  `Infrastructure.Identidad.CuentaUsuario` guarda las credenciales de Identity (usuario, correo, contraseña, bloqueo).
  Comparten el mismo identificador. Así el dominio no depende de Identity.
- **Permisos definidos en código** (`Domain.Seguridad.Permisos`). Las sedes solo los combinan en roles.
- **Roles del sistema sin permisos guardados.** Superadministrador y Administrador de sede calculan sus permisos
  desde el catálogo, así que un permiso nuevo les llega sin migraciones.
- **Reglas en el dominio** (lanzan `DominioException`): un usuario tiene al menos un rol; nunca el de superadmin;
  solo roles de su sede; los roles del sistema no se editan; un rol de sede no puede tener permisos exclusivos.
  Las reglas que dependen de quién hace la acción irán en Application.
- **Solo raíces de agregado en `IAppDbContext`**: los permisos de un rol se cambian a través del rol y los roles de un
  usuario a través del usuario.
- **Identificadores GUID v7**: únicos y ordenados por fecha, eficientes como clave en SQL Server.
