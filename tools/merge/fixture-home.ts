/**
 * The merge tool's tests' scratch folders: each under the checkout's `_fixtures/tool-tests/`, which git ignores and
 * `tools/fixtures-keep.mjs` sweeps, never the system's temporary folder or the folder a test was started in.
 */
import { mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const FIXTURE_HOME = join(fileURLToPath(new URL('../..', import.meta.url)), '_fixtures', 'tool-tests');

/** A new empty folder named for what uses it, and the call that removes it. */
export function fixtureFolder(name: string): { dir: string; remove: () => void } {
  mkdirSync(FIXTURE_HOME, { recursive: true });
  const dir = mkdtempSync(join(FIXTURE_HOME, `${name}-`));
  return { dir, remove: () => rmSync(dir, { recursive: true, force: true }) };
}
