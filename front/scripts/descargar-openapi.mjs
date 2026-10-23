// Descarga el contrato OpenAPI de la API en ejecución y lo guarda en openapi/inventario-api.json.
// Luego `npm run api:generar` crea el cliente en src/app/api/. Requiere la API corriendo (dotnet run) y
// el certificado exportado (npm run certificado), que se usa para confiar en el HTTPS de desarrollo.
import { readFileSync, writeFileSync } from 'node:fs';
import { get } from 'node:https';
import { join } from 'node:path';

const raiz = join(import.meta.dirname, '..');
const url = process.env.INVENTARIO_OPENAPI_URL ?? 'https://localhost:7180/openapi/v1.json';
const destino = join(raiz, 'openapi', 'inventario-api.json');

let ca;
try {
  ca = readFileSync(join(raiz, '.certs', 'localhost.pem'));
} catch {
  console.error('Falta el certificado de desarrollo. Ejecute primero: npm run certificado');
  process.exit(1);
}

get(url, { ca }, (respuesta) => {
  if (respuesta.statusCode !== 200) {
    console.error(`La API respondió ${respuesta.statusCode} en ${url}. ¿Está corriendo en modo Development?`);
    process.exit(1);
  }

  let cuerpo = '';
  respuesta.setEncoding('utf8');
  respuesta.on('data', (parte) => (cuerpo += parte));
  respuesta.on('end', () => {
    // Formato estable (2 espacios) para que los cambios del contrato se vean claros en git.
    writeFileSync(destino, JSON.stringify(JSON.parse(cuerpo), null, 2) + '\n', 'utf8');
    console.log(`Contrato guardado en ${destino}`);
  });
}).on('error', (error) => {
  console.error(`No se pudo conectar con ${url}: ${error.message}`);
  console.error('Arranque la API (en la carpeta back: dotnet run --project src/Inventario.Api) y vuelva a intentar.');
  process.exit(1);
});
