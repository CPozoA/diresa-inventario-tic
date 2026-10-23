// Exporta el certificado HTTPS de desarrollo de .NET (el mismo que usa la API) para que `ng serve`
// funcione en https://localhost:4200 sin advertencias del navegador. Se ejecuta una sola vez por PC.
// Los archivos quedan en front/.certs/ (no se suben al repositorio).
import { execFileSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';

const carpeta = join(import.meta.dirname, '..', '.certs');
mkdirSync(carpeta, { recursive: true });

execFileSync(
  'dotnet',
  ['dev-certs', 'https', '--export-path', join(carpeta, 'localhost.pem'), '--format', 'Pem', '--no-password'],
  { stdio: 'inherit' },
);

console.log(`Certificado exportado en ${carpeta}`);
