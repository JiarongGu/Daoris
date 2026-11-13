/**
 * The time ratchet as the merge's last check. Inside a merge the gates run beside each other and verify's ratchet stands aside, so
 * it is judged once here, after every gate has finished, on the timing records the merge's own gates wrote since it began. It
 * fails like any gate and is named in the summary.
 */

import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { join } from 'node:path';

export type RatchetRun =(since: number) => { status: number | null; output: string };
export type RatchetResult = { gate: { name: string; run: string }; verdict: 'PASS' | 'FAIL'; code: number | null; note: string; ms: number };

export const RATCHET_COMMAND = 'node tools/time-ratchet.mjs --since <the merge\'s start>';

export function ratchetStep(run: RatchetRun, since: number, now: () => number = Date.now): RatchetResult {
  const started = now();
  const ran = run(since);
  const lines = ran.output.split('\n').map((line) => line.trim()).filter(Boolean);
  const verdict = ran.status === 0 ? 'PASS' : 'FAIL';
  const said = ran.status === 0 ? (lines.at(-1) ?? 'clean') : `${lines.length} line${lines.length === 1 ? '' : 's'}: ${lines.slice(0, 3).join(' | ')}${lines.length > 3 ? ' …' : ''}`;
  return { gate: { name: 'time-ratchet', run: RATCHET_COMMAND }, verdict, code: ran.status, note: said, ms: now() - started };
}

/** The ratchet run in the checkout at `root` with `env`, as the merge's last check; a repository with no time ratchet has nothing to judge. */
export const ratchetIn = (root: string, env: NodeJS.ProcessEnv): RatchetRun => (since) => {
  const tool = join(root, 'tools', 'time-ratchet.mjs');
  if (!existsSync(tool)) return { status: 0, output: 'time-ratchet: this repository has no time ratchet, so nothing is judged\n' };
  const ran = spawnSync(process.execPath, [tool, '--since', String(since)], { cwd: root, env, encoding: 'utf8', windowsHide: true });
  return { status: ran.status, output: `${ran.stdout}${ran.stderr}` };
};

/**
 * The gates `gates` runs, and after them, once, the ratchet on the records written since they began, or since `since` where the
 * caller holds an earlier start (a rerun judges the records of the whole merge, not only of the gates it runs again).
 */
export async function withFinalRatchet<R>(
  gates: () => Promise<R[]>, ratchet: RatchetRun, now: () => number = Date.now, since: number | null = null,
): Promise<Array<R | RatchetResult>> {
  const began = now();
  const results = await gates();
  return [...results, ratchetStep(ratchet, since !== null && since < began ? since : began, now)];
}

/** The name the time check answers to in `--rerun`: it is the merge's last check, not a gate of the plan, and it runs after every rerun. */
export const RATCHET_NAME = 'time-ratchet';

/** `--rerun`'s names with the time check's taken out: the gates to run again. The check itself runs last in every rerun. */
export const gatesToRerun = (named: string[]): string[] => named.filter((name) => name !== RATCHET_NAME);
