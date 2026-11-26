import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * The family rehearsal's gate stage (`tools/family-gates.mjs`), run whole on a copy of the tracked examples with real git and the
 * real CLI on the PATH: game's declared gate, its `regenerate` first, is green at an engine pin behind engine's line and red at the
 * line's head, which drops a call game makes, while engine's own checkout stands at that head throughout.
 */

interface Ran { code: number; out: string }
const stage = await import(
  // @ts-expect-error — untyped workspace tooling
  '../../../tools/family-gates.mjs') as {
  familyGatesStage: (options: {
    examplesRoot: string; cliBin: string; scratch: string; run: (command: string, cwd: string, env?: Record<string, string>) => Ran;
    check: (label: string, ok: boolean, detail: string) => void;
  }) => void;
};

const repoRoot = dirname(dirname(dirname(dirname(fileURLToPath(import.meta.url)))));
const scratch = join(repoRoot, '_fixtures', 'family-gates-stage');

function run(command: string, cwd: string, env: Record<string, string> = {}): Ran {
  const ran = spawnSync(command, { cwd, shell: true, encoding: 'utf8', windowsHide: true, env: { ...process.env, ...env } });
  return { code: ran.status ?? 2, out: `${ran.stdout}${ran.stderr}` };
}

test('the gate stage is green on the tracked examples: game\'s gate passes at its pin where engine\'s line head fails it', () => {
  const said: { label: string; ok: boolean; detail: string }[] = [];
  stage.familyGatesStage({
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
