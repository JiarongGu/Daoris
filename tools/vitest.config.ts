/**
 * The merge tool's tests on Vitest, in two projects: `unit` (a case runs in seconds, with no process, git or clock wait; run
 * whole by `npm run verify`) and `e2e` (a case seeds a scratch repository under `_fixtures/` and runs the tool in it; run by
 * `npm run tooltests`). The scripts that run a project give its JSON report (`TOOL_REPORTS`), which the time ratchet and the
 * merge's reached checks read. Each test file runs in a process of its own (`forks`): the tool's locks and process listings
 * name the process by its pid, and a test sets the process's environment. `DAORIS_TOOL_WORKERS` (a count or a percentage)
 * caps the workers, a quarter of the machine unless it says otherwise.
 *
 * Run one file: `node node_modules/vitest/vitest.mjs run --config tools/vitest.config.ts tools/merge/flow.test.ts`.
 */
import { fileURLToPath } from 'node:url';
import type { ViteUserConfig } from 'vitest/config';

/** Each project's files and its per-test budget; the unit budget is the page's, an end-to-end case's a Process case's. */
export const TOOL_PROJECTS = Object.freeze({
  unit: { include: ['tools/merge/**/*.test.ts'], timeoutMs: 10_000 },
  e2e: { include: ['tools/merge/**/*.e2e.ts'], timeoutMs: 120_000 },
});

/** The JSON report each project's script writes, repository-relative. */
export const TOOL_REPORTS = Object.freeze({ unit: '_fixtures/test-timing/tool-unit.json', e2e: '_fixtures/test-timing/tool-e2e.json' });

const project = (name: keyof typeof TOOL_PROJECTS) => ({
  extends: true as const,
  test: { name, include: [...TOOL_PROJECTS[name].include], testTimeout: TOOL_PROJECTS[name].timeoutMs, hookTimeout: TOOL_PROJECTS[name].timeoutMs },
});

const config: ViteUserConfig = {
  root: fileURLToPath(new URL('..', import.meta.url)),
  test: {
    environment: 'node',
    globals: false,
    pool: 'forks',
    maxWorkers: process.env['DAORIS_TOOL_WORKERS'] ?? '25%',
    projects: [project('unit'), project('e2e')],
  },
};

export default config;
