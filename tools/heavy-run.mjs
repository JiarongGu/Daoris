#!/usr/bin/env node
/**
 * The heavy tier's run after a merge lands (`tools/merge/heavy-after.ts` says why and what it records), started in the background by the
 * merge queue on main's new tip, never by hand in the main checkout:
 *
 *   node tools/heavy-run.mjs --id <id> --commit <sha> --base <sha> [--branches <a,b>]
 *
 * It sets the heavy tree (`local/heavy-tree`, a worktree of its own, so main moves on beside it) to the commit, readies it as a builder's
 * worktree is (`tools/worktree-setup.mjs`), and runs `tools/reached-checks.mjs --tier heavy --base <base>` there: every Process, git
 * and rehearsal class the merge's paths reach, each pass recorded in the store the next merge replays from. What it ends with is
 * appended to `local/heavy-runs.jsonl`; a red is filed as a finding in the CI's record, which the next merge reads as main's red.
 * Exit codes: 0 green · 1 red or an error · 2 a tool error.
 */
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { isMain } from './fsx.mjs';
import { readFindings, writeFindings, fileSighting } from './ci-background.mjs';
import { HEAVY_PREFIX, HEAVY_TREE, appendHeavy, settleHeavy } from './merge/heavy-after.ts';
import { parseOutcome } from './merge/reached-classify.ts';

/** A run settled into the checkout's records: its line appended, each red filed as a finding. Answers the lines to say. */
export function settleInto(home, run, outcome, code, now = new Date().toISOString()) {
  const settled = settleHeavy(run, outcome, code, now);
  let findings = readFindings(home);
  for (const sighting of settled.sightings) findings = fileSighting(findings, sighting).record;
  if (settled.sightings.length) writeFindings(home, findings);
  appendHeavy(home, settled.run);
  return { run: settled.run, lines: settled.lines };
}

const git = (cwd, args) => spawnSync('git', args, { cwd, encoding: 'utf8', windowsHide: true });

/** The heavy tree at `commit`: made where none stands, moved there where it holds another. */
function prepareTree(checkout, commit) {
  const path = join(checkout, HEAVY_TREE);
  if (!existsSync(path)) {
    const made = git(checkout,['worktree', 'add', '--detach', path, commit]);
    if (made.status !== 0) throw new Error(`the heavy tree could not be made: ${String(made.stderr).trim()}`);
    return path;
  }
  const moved = git(path, ['checkout', '--quiet', '--detach', commit]);
  if (moved.status !== 0) throw new Error(`the heavy tree could not move to ${commit.slice(0, 7)}: ${String(moved.stderr).trim()}`);
  return path;
}

const runLogged = (command, args, cwd) => new Promise((done) => {
  let text = '';
  const child = spawn(command, args, { cwd, windowsHide: true, stdio: ['ignore', 'pipe', 'inherit'] });
  child.stdout.on('data', (chunk) => { text += chunk; process.stdout.write(chunk); });
  child.on('error', () => done({ code: null, text }));
  child.on('close', (code) => done({ code, text }));
});

const valueOf = (argv, name) => {
  const at = argv.indexOf(name);
  return at >= 0 ? (argv[at + 1] ?? null) : null;
};

async function main(argv) {
  const id = valueOf(argv, '--id');
  const commit = valueOf(argv, '--commit');
  const base = valueOf(argv, '--base');
  if (!id || !commit || !base) {
    console.error('usage: node tools/heavy-run.mjs --id <id> --commit <sha> --base <sha> [--branches <a,b>]');
    return 2;
  }
  const home = resolve(process.cwd());
  const run = { id, state: 'running', commit, base, branches: (valueOf(argv, '--branches') ?? '').split(',').filter(Boolean), at: new Date().toISOString() };
  let outcome = null;
  let code = null;
  try {
    const tree = prepareTree(home, commit);
    mkdirSync(join(tree, 'local'), { recursive: true });
    const setup = spawnSync(process.execPath, ['tools/worktree-setup.mjs'], { cwd: tree, stdio: 'inherit', windowsHide: true });
    if (setup.status !== 0) throw new Error(`the heavy tree's setup failed (exit ${setup.status})`);
    const ran = await runLogged(process.execPath, ['tools/reached-checks.mjs', '--tier', 'heavy', '--base', base], tree);
    code = ran.code;
    outcome = parseOutcome(ran.text);
  } catch (error) {
    console.error(`${HEAVY_PREFIX} ${error.message}`);
  }
  const settled = settleInto(home, run, outcome, code);
  for (const line of settled.lines) console.log(line);
  return settled.run.state === 'green' ? 0 : 1;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`heavy-run: ${error.stack ?? error.message}`);
      process.exitCode = 2;
    },
  );
}
