import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// The build lands in the HTTP host's wwwroot, so the API and the UI are ONE origin in a real
// deployment — which is what makes CORS unnecessary there, and what lets the desktop shell host
// exactly the same bytes rather than a second copy built differently.
export default defineConfig({
  // WEBFAST1: Tailwind stays out of the unit tests (vitest sets VITEST before it reads this file).
  // `css: false` below hands every test an empty stylesheet, so Tailwind's output was thrown away;
  // under the vm pool its scan also had vite transform files no test imports, the node scripts
  // among them, and a full run printed a vite warning 187 times about `scripts/e2e-host.mjs`. The
  // tests that check `tokens.css` read it from disk.
  plugins: [react(), process.env.VITEST ? null : tailwindcss()],
  build: {
    outDir: '../Daoris.Service/Daoris.Service.Http/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5178,
    // In development the two are on different ports, so the dev server proxies rather than the host
    // relaxing CORS. Same-origin in dev as well as in production means no code path differs between
    // them — the class of bug where a feature works only in one.
    proxy: { '/api': 'http://localhost:5177' },
  },
  // The fast inner loop (D42): unit tests in jsdom, inline in the vite config — the bilingual
  // sibling's proven arrangement, shims included. Playwright stays the outer loop over examples/.
  test: {
    environment: 'jsdom',
    // WEBFAST1: workers outlive a file. Under the default pool (`forks`, isolated) each of the 225
    // files started a fresh process that loaded jsdom and vitest again, a third of the tracked time.
    // A vm pool keeps the worker and runs each file in a fresh jsdom window as its own vm context,
    // with its own module instances, so no file sees another's DOM, globals or module state. The
    // cost is memory: a worker holds its files' contexts until it passes `vmMemoryLimit` (by default
    // an equal share of the machine's memory per worker) and is replaced.
    pool: 'vmThreads',
    globals: true,
    setupFiles: ['./src/test/setup.tsx'],
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
    // 🔴 A hang detector, not a speed budget. Tests that type through `userEvent` wait on a timer per
    // keystroke, and across ~60 files in parallel workers they ran 1–5s alone and 5.1–6s on a loaded
    // machine: four full runs on 2026-09-24 each timed out a DIFFERENT typing test at the 5s default,
    // and every one passed alone. A real failure is an assertion, and it still fails at once.
    testTimeout: 20_000,
  },
});
