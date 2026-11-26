import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * The family rehearsal's pin stage (`tools/family-pins.mjs`), run whole on a copy of the tracked examples with real git and the
 * real CLI: each step it says is green, and the tracked examples hold the family file it reads.
 */

interface Ran { code: number; out: string }
const stage = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/family-pins.mjs') as {
  familyPinsStage: (options: {
    examplesRoot: string; cliBin: string; scratch: string; run: (command: string, cwd: string) => Ran;
    check: (label: string, ok: boolean, detail: string) => void;
  }) => void;
};

const repoRoot = dirname(dirname(dirname(dirname(fileURLToPath(import.meta.url)))));
const scratch = join(repoRoot, '_fixtures', 'family-pins-stage');

function run(command: string, cwd: string): Ran {
  const ran = spawnSync(command, { cwd, shell: true, encoding: 'utf8', windowsHide: true });
  return { code: ran.status ?? 2, out: `${ran.stdout}${ran.stderr}` };
}

test('the pin stage is green on the tracked examples: game to engine pinned, a planted cycle and a planted branch pin refused', () => {
  const said: { label: string; ok: boolean; detail: string }[] = [];
  stage.familyPinsStage({
    examplesRoot: join(repoRoot, 'examples'),
    cliBin: join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs'),
    scratch,
    run,
    check: (label, ok, detail) => said.push({ label, ok, detail }),
  });

  assert.deepEqual(said.filter((step) => !step.ok).map((step) => `${step.label}\n${step.detail}`), []);
  assert.equal(said.length, 4);
  rmSync(scratch, { recursive: true, force: true });
});
