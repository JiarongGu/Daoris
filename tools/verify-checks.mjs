#!/usr/bin/env node
/**
 * The merge's `cli` gate, `npm run verify:checks`: the steps of `tools/verify-checks/steps.mjs`, the writers first and alone, then the
 * rest side by side (`tools/verify-checks/run.mjs`), each said with its output as it ends, every red said again at the end.
 *
 * Handed the merge's changed paths (`MERGE_BRANCH_CHANGED`, `tools/merge/gate-hands.ts`), it runs only the steps a changed path is
 * one of the reads of, and a step whose `aside` gate this repository declares stands aside; handed none (a developer's run, the full
 * set, the release workflow) it runs every step.
 *
 *   npm run verify:checks                    every step, or those the handed paths reach
 *   node tools/verify-checks.mjs --plan      what would run and why each other step would not, nothing run
 *
 * Exit codes: 0 every step that ran passed · 1 a step failed · 2 a tool error.
 */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { changedPaths } from './merge/gate-hands.ts';
import { withGateStart } from './time-ratchet.mjs';
import { runSteps, shellRun } from './verify-checks/run.mjs';
import { CHECK_STEPS, selectSteps } from './verify-checks/steps.mjs';

const declaredGates = (root) => (JSON.parse(readFileSync(join(root, 'daoris.gates.json'), 'utf8')).gates ?? []).map((gate) => gate.name);
const seconds = (ms) => `${(ms / 1000).toFixed(1)}s`;

/** The lines that say what a run takes and leaves. */
export function planLines(chosen, changed, total) {
  const by = changed ? `by the ${changed.length} path${changed.length === 1 ? '' : 's'} the merge changes` : 'every step: no changed paths were handed';
  return [`verify:checks: ${chosen.run.length} of ${total} steps, ${by}`, ...chosen.left.map(({ step, why }) => `  left: ${step.run}: ${why}`)];
}

/** Runs the gate's steps at `root`; the exit code. */
export async function verifyChecks({ root, env = process.env, steps = CHECK_STEPS, now = Date.now, run = shellRun(root, withGateStart(env, now())), say = console.log, plan = false, width }) {
  const changed = changedPaths(env);
  const chosen = selectSteps(steps, changed, { gates: changed ? declaredGates(root) : [] });
  for (const line of planLines(chosen, changed, steps.length)) say(line);
  if (plan) {
    for (const each of chosen.run) say(`  run: ${each.run}`);
    return 0;
  }
  const started = now();
  const ran = await runSteps(chosen.run, {
    run, width, now,
    ended: (step, result, ms) => {
      say(`== ${result.status === 0 ? 'ok ' : 'RED'} ${step.run} (${seconds(ms)})`);
      if (result.output.trim()) say(result.output.trimEnd());
    },
  });
  const red = ran.filter((each) => each.status !== 0);
  say(`verify:checks: ${ran.length} step${ran.length === 1 ? '' : 's'} in ${seconds(now() - started)}, ${red.length} red`);
  for (const each of red) say(`  RED ${each.step.run} (exit ${each.status})`);
  return red.length ? 1 : 0;
}

if (isMain(import.meta.url)) {
  verifyChecks({ root: dirname(dirname(fileURLToPath(import.meta.url))), plan: process.argv.includes('--plan') }).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`verify-checks: ${error.stack ?? error.message}`);
      process.exitCode = 2;
    },
  );
}
