#!/usr/bin/env node
// Boots the REAL HTTP host over the example family with a fresh scratch store, for Playwright's
// webServer. The built dll is spawned directly rather than through `dotnet run`, so killing this
// process kills the host with it — `run` wraps the app in a child that survives its parent on
// Windows, and an orphaned host makes every later run fail on the port bind.
import { spawn } from 'node:child_process';
import { mkdirSync, rmSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const webRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const repoRoot = dirname(dirname(webRoot));
const dll = join(
  repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http', 'bin', 'Debug', 'net10.0',
  'daoris-knowledge-http.dll',
);
const scratch = join(repoRoot, '_fixtures', 'web-e2e');

if (!existsSync(dll)) {
  console.error(`e2e-host: ${dll} is not built — run \`npm run test:web\` from the workspace root,`);
  console.error('which builds the web bundle and the host before the suite.');
  process.exit(2);
}

rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });

const host = spawn('dotnet', [dll], {
  stdio: 'inherit',
  // Content root is the working directory, and the static bundle lives at <content root>/wwwroot —
  // beside the project, where vite builds it. Spawned from anywhere else the API answers and the
  // page is a 404, which reads as "the app is broken" rather than "the cwd was wrong".
  cwd: join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http'),
  env: {
    ...process.env,
    DAORIS_KNOWLEDGE_ROOT: join(repoRoot, 'examples'),
    DAORIS_KNOWLEDGE_DB: join(scratch, 'knowledge.db'),
    ASPNETCORE_URLS: 'http://localhost:5199',
  },
});
host.on('exit', (code) => process.exit(code ?? 0));
