/**
 * The family rehearsal's gate stage: game's declared gate builds against the engine commit game pins, never engine's checkout as it
 * stands. Engine lands a commit, then a second that drops a call game makes, so its line's head would fail game's build; game pins
 * the first. Each gate of game's `daoris.gates.json` is run as the driver runs a declared gate, its `regenerate` first (game declares
 * `daoris family materialize` there), with the workspace's CLI first on the PATH: green at the pin, red with the pin moved to the head.
 *
 * Its own module so its end-to-end test (`src/Daoris.Cli/tool-test/family-gates.e2e.ts`) runs it whole. Each step is said through
 * the rehearsal's `check(label, ok, detail)`. It writes only under `scratch`, a copy of the examples.
 */
import { copyFileSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { copyTree } from './fsx.mjs';
import { withFirstOnPath, writeDoctrineLauncher } from './setup-kit.mjs';

const GIT = 'git -c user.name="Family Rehearsal" -c user.email=rehearsal@example.invalid -c commit.gpgsign=false -c core.autocrlf=false';

/**
 * @param {{ examplesRoot: string, cliBin: string, scratch: string,
 *   run: (command: string, cwd: string, env?: Record<string, string>) => { code: number, out: string },
 *   check: (label: string, ok: boolean, detail: string) => void }} options
 */
export function familyGatesStage({ examplesRoot, cliBin, scratch, run, check: say }) {
  rmSync(scratch, { recursive: true, force: true });
  for (const name of ['engine', 'game']) copyTree(join(examplesRoot, name), join(scratch, name));
  copyFileSync(join(examplesRoot, 'daoris.family.json'), join(scratch, 'daoris.family.json'));
  const engine = join(scratch, 'engine');
  const game = join(scratch, 'game');
  const machine = withFirstOnPath(join(scratch, 'bin'));
  writeDoctrineLauncher(join(scratch, 'bin'), cliBin);

  const commitAll = (message) => [run(`${GIT} add -A`, engine), run(`${GIT} commit -q -m "${message}"`, engine), run(`${GIT} rev-parse HEAD`, engine)];
  const steps = [run(`${GIT} init -q -b main`, engine), ...commitAll('the engine\'s first landing')];
  const landed = steps.at(-1).out.trim();
  const surface = join(engine, 'interface.json');
  const calls = JSON.parse(readFileSync(surface, 'utf8'));
  writeFileSync(surface, `${JSON.stringify({ ...calls, calls: calls.calls.filter((call) => call !== 'advance') }, null, 2)}\n`);
  steps.push(...commitAll('the engine drops advance'));
  const head = steps.at(-1).out.trim();
  say('engine lands a commit, then one that drops a call game makes', steps.every((step) => step.code === 0)
    && /^[0-9a-f]{40}$/.test(landed) && /^[0-9a-f]{40}$/.test(head) && landed !== head, steps.map((step) => step.out).join('\n'));

  const gates = JSON.parse(readFileSync(join(game, 'daoris.gates.json'), 'utf8')).gates;
  const runGates = (commit) => {
    if (commit) writeFileSync(join(game, 'daoris.family.lock'), `${JSON.stringify({ pins: [{ producer: 'engine', commit }] }, null, 2)}\n`);
    const ran = [];
    for (const gate of gates) {
      for (const command of [gate.regenerate, gate.run].filter(Boolean)) {
        ran.push(run(command, game, machine));
        if (ran.at(-1).code !== 0) return { code: ran.at(-1).code, out: ran.map((step) => step.out).join('\n') };
      }
    }
    return { code: 0, out: ran.map((step) => step.out).join('\n') };
  };

  const tracked = runGates(null);
  say('game\'s gate is green as the example tracks it, pinning nothing', tracked.code === 0 && tracked.out.includes('nothing to build against'), tracked.out);
  const pinned = runGates(landed);
  say('game\'s gate is green at an engine pin behind engine\'s line, engine\'s checkout at the head',
    gates.length > 0 && pinned.code === 0 && pinned.out.includes(`game builds against engine at ${landed.slice(0, 12)}`), pinned.out);
  const atHead = runGates(head);
  say('…and red with the pin at the line\'s head, which drops advance',
    atHead.code === 1 && atHead.out.includes(`at ${head.slice(0, 12)}: it lacks advance`), atHead.out);
}
