/**
 * The full set's replay as the merge tool wires it: the census's key context over a tree (`keyContextOf`, which the reached checks
 * read too), the store the class passes are kept in, and, for each Process half the full set runs, the run narrowed to the classes
 * no pass covers (`fullReplayOf`). The typed modules (`merge/full-replay.ts`, `merge/class-reuse.ts`) hold the decisions; this
 * reads the repository and writes the run settings.
 */
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { TS_SUITES, census, treeOf, tsReaches, unitReaches } from '../test-map.mjs';
import { privateDigest } from '../private-gate-files.mjs';
import { halfOf, pathReach, reachSettings, withSettings } from '../process-reach.mjs';
import { PassedClasses } from '../merge/class-reuse.ts';
import { toolchainOf } from '../merge/flow-deps.ts';
import { planClasses, recordPasses, replayLine } from '../merge/full-replay.ts';
import { reuseContext } from '../merge/reached-reuse.ts';

/** The census's readers the key of a reached class asks (`merge/reached-reuse.ts` takes them, being typed). */
export const READERS = { tsSuites: TS_SUITES, tsReaches, unitReaches, pathReach };

/** The file the class passes are kept in: the main checkout's `local/reached-classes.json`, or the file `DAORIS_REACHED_STORE` names. */
export const storeFile = (checkout) => process.env.DAORIS_REACHED_STORE || join(checkout, 'local', 'reached-classes.json');

/** The census of the tree at `root` with the key context over it, under the gates the tree declares. */
export function keyContextOf(root) {
  const { files, read } = treeOf(root);
  const map = census(files, read);
  const gatesText = read('daoris.gates.json');
  const gates = JSON.parse(gatesText).gates ?? [];
  const toolchain = toolchainOf();
  const context = reuseContext({ map, files, read, toolchain: `${toolchain.node}+${toolchain.dotnet}`, gates, readers: READERS });
  const privates = privateDigest(root);
  return { map, gates, gatesText, context: { ...context, declaration: (suite) => `${context.declaration(suite)}\0${privates}` } };
}

/**
 * The replay of one full set over the tree at `root`: `narrow` is asked as a Process half is about to run and answers its run
 * without the classes a standing pass covers, `settle` records the passes its report shows. A half a CI run has already narrowed
 * by reach (`gate.reach`) and one whose command names no run settings runs as declared. The census is read once, when the first
 * half asks (about a minute and a half on this repository), unless `world` is handed.
 */
export function fullReplayOf({ root, checkout, dir, commit, say = console.log, world = null }) {
  const store = new PassedClasses(storeFile(checkout));
  const pending = new Map();
  return {
    narrow(gate) {
      const half = gate.reach ? null : halfOf(gate.run);
      if (!half) return null;
      world ??= keyContextOf(root);
      const units = world.map.units.filter((unit) => unit.kind === 'dotnet' && unit.suite === gate.name);
      if (!units.length) return null;
      const plan = planClasses(units, world.context, store);
      pending.set(gate.name, { units, keys: plan.keys });
      say(`  ${replayLine(gate.name, plan)}`);
      if (!plan.replayed.length) return { run: gate.run, replay: plan };
      if (!plan.run.length) return { run: null, replay: plan };
      try {
        const file = join(dir, `${gate.name}.${basename(half.settings)}`);
        writeFileSync(file, reachSettings(readFileSync(join(root, half.settings), 'utf8'), plan.replayed.map((unit) => unit.unit)));
        return { run: withSettings(gate.run, file), replay: plan };
      } catch (error) {
        say(`  ${gate.name}: its classes are not narrowed (${error.message}), so it runs whole`);
        return { run: gate.run, replay: { ...plan, replayedCount: 0, ran: units.length } };
      }
    },
    settle(gate, result) {
      const held = pending.get(gate.name);
      if (!held || !result.trx || !existsSync(result.trx)) return;
      recordPasses({ units: held.units, keys: held.keys, trx: readFileSync(result.trx, 'utf8'), verdict: result.verdict, store, at: new Date().toISOString(), run: commit.slice(0, 7) });
      store.save();
    },
  };
}

/** What a full set replayed, as lines: the gates passed on a standing pass, and the classes left out of the halves that ran. */
export function replaySummary(results) {
  const gates = results.filter((result) => result.replayed && !result.classReplay);
  const classes = results.filter((result) => result.classReplay);
  const left = classes.reduce((sum, result) => sum + result.classReplay.replayedCount, 0);
  const lines = [`replayed: ${gates.length} of ${results.length} gates on a standing pass of the same inputs${gates.length ? ` (${gates.map((result) => result.gate.name).join(', ')})` : ''}`];
  if (classes.length) lines.push(`replayed: ${left} classes of ${classes.length} Process halves left out of their runs (${classes.map((result) => `${result.gate.name} ${result.classReplay.replayedCount}`).join(', ')})`);
  return lines;
}

/** The sweeps a full set satisfies: each Process half that passed, run or replayed by key, as strict as a whole run. */
export const sweptBy = (results, commit, at, isProcessGate) => results
  .filter((result) => result.verdict === 'PASS' && isProcessGate(result.gate))
  .map((result) => ({ gate: result.gate.name, at, commit, kind: 'full' }));
