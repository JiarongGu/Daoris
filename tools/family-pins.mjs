/**
 * The family rehearsal's pin stage: the example family's file, game's pin of a landed engine commit, and `daoris family check` run
 * from game's checkout, green on game to engine, red on a planted cycle and on a planted branch pin.
 *
 * Its own module so its end-to-end test (`src/Daoris.Cli/tool-test/family-pins.e2e.ts`) runs it whole, where the rehearsal
 * around it is a run of every real process.
 * Each step is said through the rehearsal's `check(label, ok, detail)`. It writes only under `scratch`, a copy of the examples.
 */
import { copyFileSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { copyTree } from './fsx.mjs';

const GIT = 'git -c user.name="Family Rehearsal" -c user.email=rehearsal@example.invalid -c commit.gpgsign=false';

/**
 * @param {{ examplesRoot: string, cliBin: string, scratch: string, run: (command: string, cwd: string) => { code: number, out: string },
 *   check: (label: string, ok: boolean, detail: string) => void }} options
 */
export function familyPinsStage({ examplesRoot, cliBin, scratch, run, check: say }) {
  rmSync(scratch, { recursive: true, force: true });
  for (const name of ['engine', 'game']) copyTree(join(examplesRoot, name), join(scratch, name));
  copyFileSync(join(examplesRoot, 'daoris.family.json'), join(scratch, 'daoris.family.json'));

  const engine = join(scratch, 'engine');
  const game = join(scratch, 'game');
  const landed = [
    run(`${GIT} init -q -b main`, engine),
    run(`${GIT} add -A`, engine),
    run(`${GIT} commit -q -m "the engine's first landing"`, engine),
  ];
  const head = run(`${GIT} rev-parse HEAD`, engine);
  const commit = head.out.trim();
  say('engine lands a commit on its line', landed.every((step) => step.code === 0) && head.code === 0 && /^[0-9a-f]{40}$/.test(commit),
    landed.map((step) => step.out).join('\n') + head.out);

  const pin = (value) => writeFileSync(join(game, 'daoris.family.lock'), `${JSON.stringify({ pins: [{ producer: 'engine', commit: value }] }, null, 2)}\n`);
  const check = () => run(`node "${cliBin}" family check`, game);

  pin(commit);
  const green = check();
  say('family check is green on game to engine at the pinned commit',
    green.code === 0 && green.out.includes(`game → engine at ${commit.slice(0, 12)}, on engine's line main`), green.out);

  const manifest = join(engine, 'daoris.json');
  const engineManifest = readFileSync(manifest, 'utf8');
  const planted = JSON.parse(engineManifest);
  planted.domain = { ...planted.domain, uses: ['game'] };
  writeFileSync(manifest, `${JSON.stringify(planted, null, 2)}\n`);
  const cycle = check();
  say('…red on a planted cycle, naming its path', cycle.code === 1 && /a cycle: (engine → game → engine|game → engine → game)/.test(cycle.out), cycle.out);
  writeFileSync(manifest, engineManifest);

  pin('main');
  const branch = check();
  say('…and red on a planted branch pin', branch.code === 1 && branch.out.includes('the pin `main` is a branch'), branch.out);
}
