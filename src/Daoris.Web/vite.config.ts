import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// The build lands in the HTTP host's wwwroot, so the API and the UI are ONE origin in a real
// deployment — which is what makes CORS unnecessary there, and what lets the desktop shell host
// exactly the same bytes rather than a second copy built differently.
export default defineConfig({
  plugins: [react(), tailwindcss()],
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
