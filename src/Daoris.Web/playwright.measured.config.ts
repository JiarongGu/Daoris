import { createHash } from 'node:crypto';
import { defineConfig } from '@playwright/test';

// The measured-box check (FLOWCHART1a1; the flowchart design §7, trap 3): the flowchart's stories drawn in a real browser
// and every box measured, which a unit test's DOM cannot do since jsdom lays nothing out. It serves the page's own source
// with Vite, the shipped tokens and catalogues, on a port of its own, and needs no host: `npm run e2e:measured`.

// Each checkout measures on a port of its own, its own server never reused by another: worktrees measure at once, and on
// one shared port a run reused another checkout's server and lost it mid-run when that run ended (the CI beside on
// 2b28b0112, 2026-10-20: ten stories refused their connection). `DAORIS_MEASURED_PORT` names one by hand.
const port = Number(process.env.DAORIS_MEASURED_PORT)
  || 5200 + (createHash('sha1').update(process.cwd().toLowerCase()).digest().readUInt16BE(0) % 700);

export default defineConfig({
  testDir: './measured',
  // Vite's first compile is paid once here, not by the first story of each spec (`measured/warm.ts`).
  globalSetup: './measured/warm.ts',
  timeout: 60_000,
  fullyParallel: false,
  use: {
    baseURL: `http://localhost:${port}`,
    locale: 'en-US',
    // Wide enough for the dense graph's frame at the width it needs (1440 px) beside the page's own margin.
    viewport: { width: 1600, height: 1000 },
  },
  webServer: {
    command: `npx vite --port ${port} --strictPort`,
    url: `http://localhost:${port}/measured/flow.html`,
    reuseExistingServer: false,
    timeout: 120_000,
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
