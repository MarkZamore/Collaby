import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const frontendRoot = path.resolve(__dirname, '..');
const publicDir = path.join(frontendRoot, 'public');
const outputPath = path.join(publicDir, 'runtime-version.json');

const builtAt = process.env.VITE_BUILD_TIMESTAMP || new Date().toISOString();
const buildId = process.env.VITE_BUILD_ID || process.env.GITHUB_SHA || `local-${Date.now()}`;
const commitSha = process.env.VITE_COMMIT_SHA || process.env.GITHUB_SHA || 'local';
const refName = process.env.VITE_REF_NAME || process.env.GITHUB_REF_NAME || 'local';

const payload = {
  buildId,
  commitSha,
  refName,
  builtAt,
};

await mkdir(publicDir, { recursive: true });
await writeFile(outputPath, `${JSON.stringify(payload, null, 2)}\n`, 'utf8');
