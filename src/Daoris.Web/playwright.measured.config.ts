import { defineConfig } from '@playwright/test';

// The measured-box check (FLOWCHART1a1; the flowchart design §7, trap 3): the flowchart's stories drawn in a real browser
// and every box measured, which a unit test's DOM cannot do since jsdom lays nothing out. It serves the page's own source
// with Vite, the shipped tokens and catalogues, on a port of its own, and needs no host: `npm run e2e:measured`.
export default defineConfig({
  testDir: './measured',
  // Vite's first compile is paid once here, not by the first story of each spec (`measured/warm.ts`).
  globalSetup: './measured/warm.ts',
  timeout: 60_000,
  fullyParallel: false,
  use: {
    baseURL: 'http://localhost:5181',
    locale: 'en-US',
    // Wide enough for the dense graph's frame at the width it needs (1440 px) beside the page's own margin.
    viewport: { width: 1600, height: 1000 },
  },
  webServer: {
    command: 'npx vite --port 5181 --strictPort',
    url: 'http://localhost:5181/measured/flow.html',
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
