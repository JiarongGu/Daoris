#!/usr/bin/env node
/**
 * The quick checks a builder runs before it hands back: every step of `tools/quick/steps.mjs` runs whatever an earlier one did, each
 * red is said with its output, and the end says which reds are new and which the base already stood in. No step judges test time.
 *
 *   node tools/quick.mjs                run every step; exit 0 all green, 1 a red, 2 a tool error
 *   node tools/quick.mjs --record-base  also record these verdicts as the tree's base (`local/quick-base.json`)
 *
 * The base is recorded where a session's tree is prepared, before the session gets it; a run never rewrites it.
 */
import { spawnSync } from 'node:child_process';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execGit, isMain } from './fsx.mjs';
import { quickSteps, TIME_LINE } from './quick/steps.mjs';
import { BASE_FILE, judge, LAST_FILE, readVerdicts, say as sayRed, writeVerdicts } from './quick/verdicts.mjs';

const TAIL_LINES = 40;

function shellStep(root) {
  return (command) => {
    const ran = spawnSync(command, { cwd: root, shell: true, encoding: 'utf8', windowsHide: true, maxBuffer: 256 * 1024 * 1024 });
    return { status: ran.error ? 2 : ran.status ?? 1, output: `${ran.stdout ?? ''}${ran.stderr ?? ''}${ran.error ? String(ran.error) : ''}` };
  };
}

const headOf = (root) => {
  try {
    return execGit(['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).trim();
  } catch {
    return '';
  }
};

/** Runs every step, says the reds and whose they are, writes the verdicts; the exit code. */
export function runQuick({ root, steps = quickSteps(root), run = shellStep(root), say = console.log, now = Date.now, recordBase = false }) {
  const verdicts = {};
  for (const step of steps) {
    const started = now();
    const ran = run(step);
    verdicts[step] = ran.status === 0 ? 'pass' : 'fail';
    say(`${ran.status === 0 ? 'ok  ' : 'RED '} ${step} (${((now() - started) / 1000).toFixed(1)}s)`);
    if (ran.status !== 0) say(ran.output.trimEnd().split('\n').slice(-TAIL_LINES).map((line) => `    | ${line}`).join('\n'));
  }
  const record = { head: headOf(root), at: new Date().toISOString(), steps: verdicts };
  const base = readVerdicts(join(root, BASE_FILE));
  writeVerdicts(join(root, LAST_FILE), record);
  if (recordBase) writeVerdicts(join(root, BASE_FILE), record);
  const reds = Object.values(verdicts).filter((verdict) => verdict === 'fail').length;
  say(`quick: ${steps.length} steps, ${reds} red${recordBase ? ' (recorded as the base)' : ''}`);
  say(TIME_LINE);
  if (reds > 0 && !recordBase) for (const line of sayRed(judge(verdicts, base))) say(line);
  return reds > 0 ? 1 : 0;
}

if (isMain(import.meta.url)) {
  try {
    process.exitCode = runQuick({ root: dirname(dirname(fileURLToPath(import.meta.url))), recordBase: process.argv.includes('--record-base') });
  } catch (error) {
    console.error(`quick: ${error.message}`);
    process.exitCode = 2;
  }
}
