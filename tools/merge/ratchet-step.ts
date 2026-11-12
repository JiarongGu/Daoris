/**
 * The time ratchet as the merge's last check. Inside a merge the gates run beside each other and verify's ratchet stands aside, so
 * it is judged once here, after every gate has finished, on the timing records the merge's own gates wrote since it began. It
 * fails like any gate and is named in the summary.
 */

import { spawnSync } from 'node:child_process';
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

/** The ratchet run in the checkout at `root` with `env`, as the merge's last check. */
export const ratchetIn = (root: string, env: NodeJS.ProcessEnv): RatchetRun => (since) => {
  const ran = spawnSync(process.execPath, [join(root, 'tools', 'time-ratchet.mjs'), '--since', String(since)], { cwd: root, env, encoding: 'utf8', windowsHide: true });
  return { status: ran.status, output: `${ran.stdout}${ran.stderr}` };
};

/** The gates `gates` runs, and after them, once, the ratchet on the records written since they began. */
export async function withFinalRatchet<R>(gates: () => Promise<R[]>, ratchet: RatchetRun, now: () => number = Date.now): Promise<Array<R | RatchetResult>> {
  const began = now();
  const results = await gates();
  return [...results, ratchetStep(ratchet, began, now)];
}
