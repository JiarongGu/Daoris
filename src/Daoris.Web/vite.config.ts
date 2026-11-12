import { createHash } from 'node:crypto';
import { readdirSync, rmSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

const web = fileURLToPath(new URL('.', import.meta.url));

// A module's cached transform is keyed by its own source alone, so a glob over the sources (the catalogues, the censuses) holds
// the files of its caching; the cache is one per set of source paths, and a set unused for a day is removed.
function moduleCache(): string {
  const src = join(web, 'src');
  const paths = readdirSync(src, { recursive: true, withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => relative(src, join(entry.parentPath, entry.name)).replaceAll('\\', '/'))
    .sort();
  const caches = join(web, 'node_modules', '.vitest-cache');
  const named = createHash('sha256').update(paths.join('\n')).digest('hex').slice(0, 16);
  try {
    for (const each of readdirSync(caches)) {
      const at = join(caches, each);
      if (each !== named && Date.now() - statSync(at).mtimeMs > 86_400_000) rmSync(at, { recursive: true, force: true });
    }
  } catch {
    // No cache yet.
  }
  return join(caches, named);
}

// The build lands in the HTTP host's wwwroot, so the API and the UI are ONE origin in a real
// deployment — which is what makes CORS unnecessary there, and what lets the desktop shell host
// exactly the same bytes rather than a second copy built differently.
export default defineConfig({
  // Tailwind stays out of the unit tests (vitest sets VITEST before it reads this file).
  // `css: false` below hands every test an empty stylesheet, so Tailwind's output is thrown away;
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
  // The fast inner loop: unit tests in jsdom, inline in the vite config — the bilingual
  // sibling's proven arrangement, shims included. Playwright stays the outer loop over examples/.
  test: {
    environment: 'jsdom',
    // Workers outlive a file. Under the default pool (`forks`, isolated) each of the 225
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
    // The one budget for a test and its hooks: ten seconds, over the 6 s a typing test reached on a loaded machine (the 5 s default
    // failed a different one each run). tools/time-ratchet.mjs holds each test to the same ten seconds from the durations the run
    // records. A test needing longer says why where it is declared (`names.test.ts`, WorkFrame's free-answer test) and is listed there.
    testTimeout: 10_000,
    hookTimeout: 10_000,
    // A quarter of the machine, not all of it. By default every logical processor but one ran a worker (21 on the
    // owner's machine), beside the installed window, the antivirus and the other gates, and on 2026-10-15 three gate runs
    // failed on 20 s timeouts in files that passed alone. `DAORIS_WEB_WORKERS` (a count or a percentage) says otherwise.
    maxWorkers: process.env.DAORIS_WEB_WORKERS ?? '25%',
    // Transforming the modules takes 40-71% of a run's tracked time and is redone by every gate that runs vitest (the rules, the unit
    // run): the transformed modules persist and are reused by the next process while the same source files stand.
    fsModuleCache: true,
    fsModuleCachePath: process.env.VITEST ? moduleCache() : undefined,
  },
});
