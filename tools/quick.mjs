#!/usr/bin/env node
/**
 * The quick checks a builder runs before it hands back: every step of `tools/quick/steps.mjs` runs whatever an earlier one did, the
 * writers first and the rest side by side as the `cli` gate runs them (`tools/verify-checks/run.mjs`), each red is said with its output,
 * and the end says which reds are new and which the base already stood in. No step judges test time.
 *
 *   node tools/quick.mjs                run every step; exit 0 all green, 1 a red, 2 a tool error
 *   node tools/quick.mjs --record-base  run every step at the base instead and record it (`local/quick-base.json`)
 *
 * The base is main at the merge base of HEAD with main (`tools/quick/base.mjs`), never the tree as it stands.
 */
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execGit, isMain } from './fsx.mjs';
import { quickSteps, TIME_LINE, WRITERS } from './quick/steps.mjs';
import { baseVerdicts } from './quick/base.mjs';
import { judge, LAST_FILE, say as sayRed, writeVerdicts } from './quick/verdicts.mjs';
import { runSteps, shellRun } from './verify-checks/run.mjs';

const TAIL_LINES = 40;

const headOf = (root) => {
  try {
    return execGit(['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim();
  } catch {
    return '';
  }
};

/** Runs every step, says the reds and whose they are, writes the verdicts; the exit code. */
export async function runQuick({ root, steps = quickSteps(), run = shellRun(root), say = console.log, now = Date.now, recordBase = false, width }) {
  if (recordBase) return recordTheBase(root, steps, say);
  const verdicts = {};
  const ended = (step, ran, ms) => {
    say(`${ran.status === 0 ? 'ok  ' : 'RED '} ${step.run} (${(ms / 1000).toFixed(1)}s)`);
    if (ran.status !== 0) say(ran.output.trimEnd().split('\n').slice(-TAIL_LINES).map((line) => `    | ${line}`).join('\n'));
  };
  const ran = await runSteps(steps.map((step) => ({ run: step, writes: WRITERS.includes(step) })), { run, width, now, ended });
  for (const { step, status } of ran) verdicts[step.run] = status === 0 ? 'pass' : 'fail';
  const record = { head: headOf(root), at: new Date().toISOString(), steps: verdicts };
  writeVerdicts(join(root, LAST_FILE), record);
  const red = Object.keys(verdicts).filter((step) => verdicts[step] === 'fail');
  say(`quick: ${steps.length} steps, ${red.length} red`);
  say(TIME_LINE);
  if (red.length > 0) for (const line of sayRed(judge(verdicts, baseVerdicts(root, red)))) say(line);
  return red.length > 0 ? 1 : 0;
}

/** `--record-base`: every step run at the base, the tree's own not; 1 where the base is red. */
function recordTheBase(root, steps, say) {
  const base = baseVerdicts(root, steps);
  if (!base) say('quick: no merge base with main, no base recorded');
  const reds = base ? steps.filter((step) => base.steps[step] === 'fail').length : 0;
  say(`quick: base ${base?.head ?? 'none'}: ${steps.length} steps, ${reds} red (recorded as the base)`);
  return reds > 0 ? 1 : 0;
}

if (isMain(import.meta.url)) {
  runQuick({ root: dirname(dirname(fileURLToPath(import.meta.url))), recordBase: process.argv.includes('--record-base') }).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`quick: ${error.message}`);
      process.exitCode = 2;
    },
  );
}
