import { test } from 'node:test';
import assert from 'node:assert/strict';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { rmSync } from 'node:fs';
import { familyPinsStage } from './family-pins.mjs';
import { rehearsalRun } from './rehearsal-kit.mjs';

/**
 * The family rehearsal's pin stage, run whole on a copy of the tracked examples: each step it says is green, and the tracked
 * examples hold the family file it reads.
 */

const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
const scratch = join(repoRoot, '_fixtures', 'family-pins-stage');

test('the pin stage is green on the tracked examples: game to engine pinned, a planted cycle and a planted branch pin refused', () => {
  const said = [];
  familyPinsStage({
    examplesRoot: join(repoRoot, 'examples'),
    cliBin: join(repoRoot, 'src', 'Daoris.Cli', 'bin', 'daoris.mjs'),
    scratch,
    run: rehearsalRun({ within: scratch }),
    check: (label, ok, detail) => said.push({ label, ok, detail }),
  });

  assert.deepEqual(said.filter((step) => !step.ok).map((step) => `${step.label}\n${step.detail}`), []);
  assert.equal(said.length, 4);
  rmSync(scratch, { recursive: true, force: true });
});
