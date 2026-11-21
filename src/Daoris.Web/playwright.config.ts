import { defineConfig } from '@playwright/test';
import { freeE2ePort } from './scripts/e2e-port.mjs';

// The example family is the UI's fixture: the suite boots the REAL HTTP host over examples/
// with a scratch store, and drives the same bundle a person uses — the host serves wwwroot — not a
// dev server with different behaviour. Build first; `npm run test:web` at the workspace root does.
const port = await freeE2ePort();

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  fullyParallel: false,
  use: {
    baseURL: `http://localhost:${port}`,
    locale: 'en-US',
  },
  webServer: {
    command: 'node scripts/e2e-host.mjs',
    url: `http://localhost:${port}/api/status`,
    reuseExistingServer: false,
    timeout: 120_000,
    env: { DAORIS_E2E_PORT: String(port) },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
