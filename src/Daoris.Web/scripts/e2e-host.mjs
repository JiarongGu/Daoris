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
const { copyTree } = await import(`file://${join(repoRoot, 'tools', 'fsx.mjs')}`);
const dll = join(
  repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http', 'bin', 'Debug', 'net10.0',
  'daoris-knowledge-http.dll',
);
const scratch = join(repoRoot, '_fixtures', 'web-e2e');
const family = join(scratch, 'family');

if (!existsSync(dll)) {
  console.error(`e2e-host: ${dll} is not built — run \`npm run test:web\` from the workspace root,`);
  console.error('which builds the web bundle and the host before the suite.');
  process.exit(2);
}

rmSync(scratch, { recursive: true, force: true });
mkdirSync(scratch, { recursive: true });

// The host roots at a scratch COPY of the example family, not the tracked tree: the suite includes a
// project being born mid-run (D44), and a newcomer must never dirty tracked examples.
for (const name of ['engine', 'game']) {
  copyTree(join(repoRoot, 'examples', name), join(family, name));
}

const host = spawn('dotnet', [dll], {
  stdio: 'inherit',
  // Content root is the working directory, and the static bundle lives at <content root>/wwwroot —
  // beside the project, where vite builds it. Spawned from anywhere else the API answers and the
  // page is a 404, which reads as "the app is broken" rather than "the cwd was wrong".
  cwd: join(repoRoot, 'src', 'Daoris.Service', 'Daoris.Service.Http'),
  env: {
    ...process.env,
    DAORIS_KNOWLEDGE_ROOT: family,
    DAORIS_KNOWLEDGE_DB: join(scratch, 'knowledge.db'),
    // 5196, deliberately apart from the family rehearsal's 5197–5199: the two suites run in the same
    // CI job, and an orphaned host on a shared port makes one gate's readiness probe answer against
    // the other's server — a failure that reads as flakiness rather than a port clash.
    ASPNETCORE_URLS: 'http://localhost:5196',
    // Hermetic like the family rehearsal (its own prelude states the argument): a real
    // ~/.daoris/remote.json on the developer's machine must never leak a deployment into a gate run.
    DAORIS_REMOTE_CONFIG: join(scratch, 'no-remote.json'),
  },
});
host.on('exit', (code) => process.exit(code ?? 0));
