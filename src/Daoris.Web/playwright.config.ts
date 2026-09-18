import { defineConfig } from '@playwright/test';

// The example family (D39) is the UI's fixture: the suite boots the REAL HTTP host over examples/
// with a scratch store, and drives the same bundle a person uses — the host serves wwwroot — not a
// dev server with different behaviour. Build first; `npm run test:web` at the workspace root does.
export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  fullyParallel: false,
  use: {
    baseURL: 'http://localhost:5199',
    locale: 'en-US',
  },
  webServer: {
    command: 'node scripts/e2e-host.mjs',
    url: 'http://localhost:5199/api/status',
    reuseExistingServer: false,
    timeout: 120_000,
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
