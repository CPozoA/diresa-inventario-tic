# Inventario TIC

Sistema web para el registro y control del inventario de Tecnologías de la Información y Comunicación de las sedes.

| Carpeta | Contenido |
|---------|-----------|
| [`docs/`](docs/) | Documentación. Empieza por [ARQUITECTURA.md](docs/ARQUITECTURA.md). |
| [`back/`](back/) | API en .NET 10 (ASP.NET Core) con SQL Server. |
| [`front/`](front/) | Aplicación web en Angular 22 con Angular Material. |

## Requisitos para desarrollar

- SDK de **.NET 10**: comprobar con `dotnet --list-sdks`
- **Node.js 24 LTS** (mínimo 22.22.3 o 24.15, lo exige Angular 22): comprobar con `node -v`
- **SQL Server** (Developer, Express o LocalDB)
- Editor: Visual Studio (versión compatible con .NET 10) o VS Code

## Estado

Paso 7C-4 terminado: antivirus, tarjeta gráfica, pantalla, puertos y accesorios se eligen de listas, y los campos escritos sugieren lo ya registrado. Antes: procesador y generación de lista y búsqueda por IP o nombre de equipo; registrar y editar activos desde la pantalla; lista del inventario y ficha técnica; cada tipo de activo indica qué características lleva (D43); API de activos (ficha con discos y RAM, garantía y renovación), establecimientos de salud con importación desde Excel, y catálogos (áreas por sede y catálogos generales). Ver el plan en [ARQUITECTURA.md §11](docs/ARQUITECTURA.md#11-plan-por-pasos).

## Control de versiones

Se recomienda versionar con **Git** desde la carpeta raíz (`git init`). El archivo `.gitignore` ya excluye `bin`, `obj`, `node_modules` y los archivos con secretos.
