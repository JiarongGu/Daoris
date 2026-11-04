import { spawnSync } from 'node:child_process';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { refusal } from './staging.mjs';

/** The command that runs every gate on the checkout as it stands: what a refusal names. `merge-branch`'s `FULL_COMMAND`. */
export const GATE_COMMAND = 'node tools/merge-branch.mjs --full';

/**
 * The line `merge-branch --passed` ends a refusal with (GATE6b): what to run and the smallest command that runs it,
 * `--stale`, `--rerun <gate>…` or `--full`. Read from its output, as the rest is: the publish never imports the tool.
 */
const REMEDY = /^\s*Run (.+?) on it: (node tools\/merge-branch\.mjs .+?)\s*$/;

/**
 * Whether `to` is the workspace's scratch, `_fixtures` or a folder inside it: where the deployment rehearsal
 * publishes. Compared by path, so a sibling whose name starts the same is not inside; case-blind on Windows.
 */
export function insideFixtures(to, repoRoot) {
  const rel = relative(join(resolve(repoRoot), '_fixtures'), resolve(to));
  return rel !== '..' && !rel.startsWith(`..${sep}`) && !isAbsolute(rel);
}

/**
 * Ask the merge tool whether the full set passed the checkout as it stands: its exit (0 passed, 1 not,
 * anything else could not tell) and what it printed, gate by gate. A process rather than an import, so
 * nothing the deployment rehearsal runs imports the merge tool.
 */
export function askGates(repoRoot, tool = join(dirname(dirname(fileURLToPath(import.meta.url))), 'merge-branch.mjs')) {
  const asked = spawnSync(process.execPath, [tool, '--passed'], { cwd: repoRoot, encoding: 'utf8', windowsHide: true });
  if (asked.error) return { code: 2, out: `node could not start: ${asked.error.message}` };
  return { code: asked.status ?? 2, out: `${asked.stdout ?? ''}${asked.stderr ?? ''}`.trim() };
}

const indented = (text) => text.split(/\r?\n/).filter((line) => line.trim()).map((line) => `  ${line.trimEnd()}`).join('\n');

/**
 * What stops a publish into `to` for want of gates (GATE3), as the sentence to print, and what to say when it
 * goes ahead: nothing is asked for a folder under `_fixtures`; a tree the full set passed publishes; any other
 * is refused, naming what has not passed and the smallest command the tool names that would pass it (GATE6b), or
 * the one that runs it all, unless `force` (`--force-ungated`, the person's explicit override), when it publishes
 * and says it is ungated.
 */
export function ungatedRefusal(to, repoRoot, { force = false, ask = askGates, remedy = null } = {}) {
  if (insideFixtures(to, repoRoot)) return { refusal: null, note: null };
  const { code, out } = ask(repoRoot);
  if (code === 0) return { refusal: null, note: `desktop-publish: ${out.split(/\r?\n/)[0].replace(/^merge-branch: /, '')}` };
  const why = code === 1
    ? 'the full set of gates has not passed this checkout'
    : 'could not tell whether the full set passed this checkout';
  if (force) {
    return { refusal: null, note: `desktop-publish: publishing ungated (--force-ungated): ${why}.\n${indented(out)}` };
  }
  // The tool's own last line names the command too; it is said once, below. A tool that names none, or cannot tell, gets the full set.
  // CI1: a stage from the gate tree is handed its own `remedy`, the CI's command, which runs there what the commit's verdicts lack.
  const lines = out.split(/\r?\n/);
  const named = lines.map((line) => REMEDY.exec(line)).find(Boolean);
  const [what, command] = remedy ? [remedy.what, remedy.command] : named ? [named[1], named[2]] : ['every gate', GATE_COMMAND];
  const where = remedy?.where ?? 'on this checkout';
  const said = lines.filter((line) => !REMEDY.test(line) && !line.includes(GATE_COMMAND)).join('\n');
  return {
    refusal: `desktop-publish: ${why}, so it is not published to \`${to}\`.\n${indented(said)}\n`
      + `  A merge runs only the gates its lanes reach (GATE3); run ${what} ${where}: ${command}\n`
      + '  Or, as your explicit call, publish it ungated with --force-ungated.',
    note: null,
  };
}

// ---------------------------------------------------------------------------------------------
// GATEWT1: a stage built from the gate tree, never from the moving main checkout
