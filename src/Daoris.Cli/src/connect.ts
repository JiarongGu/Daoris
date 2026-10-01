// `connect` — the registration half of the management class (D50). An explicit, opt-in registration
// with a knowledge service, never run by a gate and never on the path of anything that is; a
// repository that never runs it loses nothing but discoverability.
//
// The network itself lives in `service.ts`, which is the one module the offline-discipline tests name.
// D8's guarantee is about the DOCTRINE operations — `check`, `sync`, `index`, `upstream` — all of
// which are pure local hashing against the lock and stay that way.
//
// 🔴 A TWIN of the driver's `LineRegistration.cs` (WSSETUP5, D124 §3.3): the driver composes this same body from
// a repository's LINE and registers it with no `connect` run. What is sent (`registration()`), and what is refused
// before anything is sent (`readManifest`, `isDeclared`, `readLanes`), are held by `connect-twin.test.ts` here and
// `LineRegistrationTests` there, row for row. A change to either is a change to both.

import { existsSync, readFileSync, statSync } from 'node:fs';
import { isAbsolute, resolve } from 'node:path';
import { flagValue } from './args.ts';
import { readManifest } from './config.ts';
import { DaorisError } from './errors.ts';
import { readLanes } from './lanes.ts';
import type { LaneWords } from './lanes.ts';
import { endpoint, isLocalService, refusal, request } from './service.ts';
import type { CommandArgs, Domain, Manifest } from './types.ts';
import type { ExitCode } from './errors.ts';

const REGISTRY_PATH = '/api/registry';

/** A directory's name — the repository's identity everywhere in Daoris. */
export function repositoryName(root: string): string {
  return root.replace(/[\\/]+$/, '').split(/[\\/]/).pop() ?? 'unknown';
}

/**
 * What this repository tells a service about itself.
 *
 * @remarks
 * Sent rather than scanned because a **remote** service cannot see the repository at all. A service
 * running on this machine can read manifests off disk, and does; one running anywhere else has no such
 * option, and pretending otherwise would make the hosted deployment a second-class citizen.
 *
 * The root travels only to a **local** service (D46): it is what the driver spawns a session in, and it
 * is a machine path — `sensitive-info` keeps those out of tracked files, and the same judgement keeps
 * them off the network. A remote deployment gets the declaration and nothing about anyone's disk.
 *
 * The manifest's remote declaration travels too (D47 §4): a sync loop feeds only what the repository
 * itself, under review, said may leave. Sent as explicit booleans — silence in the manifest becomes
 * `false` on the wire, so a service never guesses what an absent field meant.
 *
 * The workspace is the opposite shape, deliberately (D48 as amended). It is **wiring**, like a git
 * remote: it lives in the machine's registry, comes from `--workspace` rather than from any tracked
 * file, and is **omitted entirely** when unstated — because an absent field PRESERVES the existing
 * row, while an explicit `null` or `""` would re-point every repository to the default on the next
 * ordinary sync tick. Silence here means "I am not saying", not "I say default".
 *
 * The lanes (D115 §2.2, DEV4) travel as their words from `daoris.lanes.json`, and always as a list:
 * none is `[]`. The service keeps a row's lanes when a registration says nothing of them (the page's
 * add, an older client), so connect, the one door that reads the file, is the one that says "none".
 */
export function registration(
  root: string, manifest: Manifest, name: string, serviceUrl: string, workspace?: string,
  lanes: LaneWords[] = [],
): {
  repository: string;
  packs: string[];
  domain: Domain | null;
  join: boolean;
  shareKnowledge: boolean;
  lanes: LaneWords[];
  root?: string;
  workspace?: string;
} {
  return {
    repository: name,
    packs: manifest.packs,
    domain: manifest.domain ? declaredDomain(manifest.domain, name) : null,
    join: manifest.remote?.join ?? false,
    shareKnowledge: manifest.remote?.knowledge ?? false,
    lanes,
    ...(isLocalService(serviceUrl) ? { root } : {}),
    ...(workspace ? { workspace } : {}),
  };
}

/**
 * What a repository says it uses (D91), read by the rule the service's `Declared.Uses` holds too, by
 * its own test table (twins): each name trimmed, a blank dropped, a repeat in any case dropped with
 * the first spelling kept, and the repository's own name dropped. Absent is empty.
 */
export function usesOf(domain: Domain | undefined | null, repository: string): string[] {
  const kept: string[] = [];
  for (const raw of Array.isArray(domain?.uses) ? domain.uses : []) {
    if (typeof raw !== 'string') continue;
    const name = raw.trim();
    const same = (other: string) => other.localeCompare(name, undefined, { sensitivity: 'accent' }) === 0;
    if (!name || same(repository) || kept.some(same)) continue;
    kept.push(name);
  }
  return kept;
}

/**
 * The declaration as it goes on the wire: `uses` by its rule, and only when it says something, so a
 * repository that declares none registers exactly as it did before the field existed.
 */
function declaredDomain(domain: Domain, repository: string): Domain {
  const { uses: _declared, ...rest } = domain;
  const uses = usesOf(domain, repository);
  return uses.length > 0 ? { ...rest, uses } : rest;
}

/** True when the domain says enough for a sibling to know what is worth asking. */
export function isDeclared(domain: Domain | undefined | null): boolean {
  if (!domain) return false;
  return Boolean(domain.summary?.trim()) || domain.owns.length > 0 || domain.accepts.length > 0;
}

/**
 * The main tree this checkout is a linked worktree OF — or null when it is a main tree, a submodule,
 * or not a git repository at all.
 *
 * @remarks
 * Git marks a linked worktree itself: its `.git` is a **file** naming the main repository's
 * `.git/worktrees/<name>` directory. Reading that file is the whole check — no spawn, which is what
 * keeps the CLI's discipline intact (only `toolchain.ts` spawns anything). A **submodule** wears a
 * `.git` file too, pointing at `.git/modules/<name>` — and a submodule is a repository of its own,
 * entirely registrable, so only the worktree marker counts.
 */
export function linkedWorktreeMain(root: string): string | null {
  const marker = resolve(root, '.git');
  try {
    if (!existsSync(marker) || statSync(marker).isDirectory()) return null;

    const pointed = /^gitdir:\s*(.+)\s*$/m.exec(readFileSync(marker, 'utf8'))?.[1]?.trim();
    if (!pointed) return null;

    const gitdir = isAbsolute(pointed) ? pointed : resolve(root, pointed);
    // <main>/.git/worktrees/<name> — three levels up is the main tree. Normalized separators, because
    // git writes forward slashes on every platform and a person may have hand-edited either kind in.
    const parts = gitdir.replaceAll('\\', '/').split('/');
    const at = parts.lastIndexOf('worktrees');
    if (at < 2 || parts[at - 1] !== '.git') return null;

    return parts.slice(0, at - 1).join('/');
  } catch {
    // An unreadable marker is not this command's to diagnose: git itself will refuse next, with a
    // better sentence than a guess here would be.
    return null;
  }
}

export async function commandConnect({ root, argv, write }: CommandArgs): Promise<ExitCode> {
  // Before anything else, including --dry-run: a registration re-pointed at an ephemeral tree keeps
  // working right up until that tree is removed, and then the repository's root is a path that does
  // not exist (D51). The refusal names where to run this instead.
  const main = linkedWorktreeMain(root);
  if (main !== null) {
    throw new DaorisError(
      'this is a linked worktree — a session\'s scratch checkout, not the repository\'s home. '
      + 'Registering it would point the machine\'s registry at a tree that is removed when its '
      + `session's work is merged. Run \`daoris connect\` from the main tree instead:\n  ${main}`,
      1);
  }

  const manifest = readManifest(root);
  const name = repositoryName(root);

  if (!isDeclared(manifest.domain)) {
    // Registering an empty declaration is worse than not registering: it puts the repository on the
    // map as something that answers nothing, and a sibling reading that learns less than from a gap.
    throw new DaorisError(
      `${name} has not said what it is. Fill in 'domain' in daoris.json first:\n`
      + "  summary — one line, for someone who has never opened this repository\n"
      + '  owns    — the areas where a change belongs here rather than anywhere else\n'
      + '  accepts — the kinds of work it is worth asking of you\n'
      + '  That declaration is how siblings know whether a quest is yours.',
      1);
  }

  // Read before anything is sent, --dry-run included: an unreadable file is refused naming each problem.
  const lanes = readLanes(root) ?? [];
  const { url } = endpoint();
  const body = registration(root, manifest, name, url, flagValue(argv, '--workspace'), lanes);
  if (argv.includes('--dry-run')) {
    write(JSON.stringify(body, null, 2));
    write(`daoris: would register with ${url}${REGISTRY_PATH}`);
    return 0;
  }
  const { status, json } = await request('POST', REGISTRY_PATH, body);
  if (status < 200 || status >= 300) {
    throw new DaorisError(`the service refused the registration: ${refusal(status, json)}`);
  }

  // The workspace the service says TOOK, not the one that was asked for — silence preserves whatever
  // the row already held, so the only honest way to report the wiring is to read it back. A service
  // too old to answer with one simply goes unmentioned rather than being guessed at.
  const landed = json as { workspace?: string } | null;

  write(`daoris: registered ${name} with ${url}`);
  write(`  owns ${manifest.domain!.owns.length} area(s); accepts ${manifest.domain!.accepts.length} kind(s)`);
  const uses = usesOf(manifest.domain, name);
  if (uses.length > 0) write(`  uses ${uses.join(', ')}`);
  if (lanes.length > 0) {
    const named = lanes.map((lane) => `${lane.id}${lane.steward ? ' (the steward\'s)' : ''}`).join(', ');
    write(`  lanes ${named}: a quest addresses one as ${name}:<lane>, or several as ${name}:<lane>+<lane>`);
  }
  if (landed?.workspace) write(`  workspace: ${landed.workspace} — this machine's wiring; nothing was written here`);
  write('  siblings can now address quests here, and see what is worth asking.');
  return 0;
}
