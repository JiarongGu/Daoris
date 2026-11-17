/**
 * Runs a list of check steps, for the `cli` gate (`tools/verify-checks.mjs`) and for `npm run quick`: each writer first, alone, in its
 * order, since the others read what it writes; then the rest side by side, `width` at a time, each started in its order. Every step runs
 * whatever an earlier one did.
 */
import { spawn } from 'node:child_process';
import { availableParallelism } from 'node:os';

/** The steps run at once: most are one node process reading files, so a few beside the merge's other gates. */
export const WIDTH = Math.max(2, Math.min(6, availableParallelism() - 1));

/**
 * Runs `steps` (`{ run, writes }`) by `run(command)`, which answers `{ status, output }` or a promise of it; `ended(step, result, ms)`
 * is told as each ends, in the order they end. Answers every step with its result and its time, in the list's order.
 */
export async function runSteps(steps, { run, width = WIDTH, now = Date.now, ended = () => {} }) {
  const results = new Map();
  const one = async (step) => {
    const started = now();
    const result = await run(step.run);
    const ms = now() - started;
    results.set(step, { ...result, ms });
    ended(step, result, ms);
  };
  for (const step of steps.filter((each) => each.writes)) await one(step);
  const rest = steps.filter((each) => !each.writes);
  let next = 0;
  const lane = async () => {
    while (next < rest.length) await one(rest[next++]);
  };
  await Promise.all(Array.from({ length: Math.min(Math.max(1, width), rest.length) }, lane));
  return steps.map((step) => ({ step, ...results.get(step) }));
}

/** A command run by the shell at `root` with `env`, its output kept whole: `{ status, output }`, status 2 where it could not start. */
export const shellRun = (root, env = process.env) => (command) => new Promise((done) => {
  const chunks = [];
  const child = spawn(command, { cwd: root, env, shell: true, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  child.stdout.on('data', (chunk) => chunks.push(chunk));
  child.stderr.on('data', (chunk) => chunks.push(chunk));
  child.on('error', (error) => done({ status: 2, output: `${Buffer.concat(chunks).toString('utf8')}${error}` }));
  child.on('close', (code) => done({ status: code ?? 1, output: Buffer.concat(chunks).toString('utf8') }));
});
