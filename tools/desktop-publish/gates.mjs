import { isAbsolute, join, relative, resolve, sep } from 'node:path';
import { askTiers } from './tier-evidence.mjs';

/**
 * Whether `to` is the workspace's scratch, `_fixtures` or a folder inside it: where the deployment rehearsal
 * publishes. Compared by path, so a sibling whose name starts the same is not inside; case-blind on Windows.
 */
export function insideFixtures(to, repoRoot) {
  const rel = relative(join(resolve(repoRoot), '_fixtures'), resolve(to));
  return rel !== '..' && !rel.startsWith(`..${sep}`) && !isAbsolute(rel);
}

const indented = (text) => text.split(/\r?\n/).filter((line) => line.trim()).map((line) => `  ${line.trimEnd()}`).join('\n');

/**
 * What stops a publish into `to` for want of a passed commit, as the sentence to print, and what to say when it goes ahead: nothing
 * is asked for a folder under `_fixtures`; a commit whose merge passed the fast tier and whose heavy run is green (or red only on
 * classes already filed as main's red) publishes; any other is refused, naming each thing it lacks and the command that supplies
 * it, unless `force` (`--force-ungated`, the person's explicit override), when it publishes and says it is ungated. `home` is the
 * checkout whose records are read and `commit` the commit staged (HEAD of `repoRoot` where none is named).
 */
export function ungatedRefusal(to, repoRoot, { force = false, ask = askTiers, home = repoRoot, commit = null } = {}) {
  if (insideFixtures(to, repoRoot)) return { refusal: null, note: null };
  const { code, out, remedy } = ask(repoRoot, { home, commit });
  if (code === 0) return { refusal: null, note: `desktop-publish: ${out.split(/\r?\n/)[0]}` };
  const why = code === 1 ? 'its tiers have not both passed' : 'could not tell whether its tiers passed';
  if (force) {
    return { refusal: null, note: `desktop-publish: publishing ungated (--force-ungated): ${why}.\n${indented(out)}` };
  }
  return {
    refusal: `desktop-publish: ${why}, so it is not published to \`${to}\`.\n${indented(out)}\n`
      + (remedy ? `  To supply ${remedy.what}: ${remedy.command}\n` : '')
      + '  Or, as your explicit call, publish it ungated with --force-ungated.',
    note: null,
  };
}

// ---------------------------------------------------------------------------------------------
// A stage built from the gate tree, never from the moving main checkout
