// Copies the production build into the API's wwwroot so the API serves the app (works on Windows, Linux, macOS).
import { cpSync, existsSync, rmSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const source = resolve(root, 'dist/frontend/browser');
const target = resolve(root, '../backend/src/BatchReleaseIQ.Api/wwwroot');
if (!existsSync(source)) { console.error(`Build output not found: ${source}`); process.exit(1); }
rmSync(target, { recursive: true, force: true });
cpSync(source, target, { recursive: true });
console.log(`Copied ${source} -> ${target}`);
