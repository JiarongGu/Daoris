import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText, writeTextAtomic } from './fsx.ts';
import { DaorisError } from './errors.ts';
import { DEFAULT_HARNESS, resolveHarness } from './harness.ts';
import { checkRooms, checkTarget } from './layout.ts';
import { checkDocuments } from './documents.ts';
import type { Lock, Manifest } from './types.ts';

export const MANIFEST_FILE = 'daoris.json';
const LOCK_FILE = 'daoris.lock';
const LOCK_VERSION = 1;

// `harness` names which agent layout to generate. It defaults rather than being required, because a
// manifest written before harnesses existed must keep working: absent is `claude-code`, the older
// layout, and `agents` is chosen by name (D117).
/**
 * `coreBudgetBytes` is what a repository is willing to pay, in always-loaded bytes, on every task.
 *
 * 30000 rather than the original 24000, and the reason is a design error the release rehearsal caught:
 * at 24000, core plus ONE pack already measured 24061, so a clean adopter's very first `check` failed
 * before they had written a single rule of their own. Worse, the gate then fired on the CANON — adding
 * a core rule broke a consumer — which is backwards. The budget exists to constrain what a repository
 * chooses to carry, not to cap what the doctrine may contain.
 *
 * 30000 leaves core, an index and two packs comfortably inside, so the gate fires on the case that
 * actually matters: a repository's OWN always-loaded material getting fat. That is the case it has
 * earned its keep on — it caught a 45% overage on first contact with one adopter, and forced the
 * retirement of an 8.3 KB duplicated rule in another.
 *
 * 🔴 **Since D59 that is no longer what is measured.** The always-loaded tier became Daoris's region
 * of `AGENTS.md`, and `inspect` counts that region's body alone — the canon's rules and the index. A
 * repository's own always-loaded material (the rest of `AGENTS.md`, `CLAUDE.md`, a local
 * `.claude/rules/` file) is not counted, and `analyze` still projects the pre-D59 quantity. What the
 * number should cap is open in `TASKS.md` (REV3 CLI F10).
 */
export const DEFAULT_CORE_BUDGET_BYTES = 30000;

const DEFAULTS: Pick<Manifest, 'packs' | 'harness' | 'coreBudgetBytes'> & { target: string | null } =
  { packs: [], harness: DEFAULT_HARNESS, target: null, coreBudgetBytes: DEFAULT_CORE_BUDGET_BYTES };

/**
 * JSON.parse behind the exit-code contract: a corrupt manifest or lock is a TOOL error (2), and a bare
 * SyntaxError escaping to Node's top level reports 1 — which a build gate reads as policy failure,
 * the one thing a broken file is not.
 */
function parseJson<T>(file: string, text: string): T {
  try {
    return JSON.parse(text) as T;
  } catch (error) {
    throw new DaorisError(`${file} is not valid JSON — ${(error as Error).message}`);
  }
}

export function readManifest(root: string): Manifest {
  const file = join(root, MANIFEST_FILE);
  if (!existsSync(file)) {
    throw new DaorisError(`no ${MANIFEST_FILE} in '${root}' — run 'daoris init' first`);
  }
  const text = readText(file);
  const parsed = parseJson<Partial<Manifest>>(file, text);
  // A JSON `"remote": null` means what absence means — local, silently — not a crash on the
  // dereference below. The type says the field is never null; the file is under no such obligation.
  if ((parsed as Record<string, unknown>).remote === null) delete parsed.remote;
  const manifest = { ...DEFAULTS, ...parsed } as Manifest;
  if (!manifest.source) throw new DaorisError(`${MANIFEST_FILE} has no 'source'`);

  // Knowledge feeds only from a joined repository (D47 §4): a manifest saying "share my knowledge but
  // do not join" has no meaning a service could honour, so it fails here — at the edge, with the fix —
  // rather than being silently narrowed somewhere a reviewer never sees.
  if (manifest.remote !== undefined) {
    const join = Boolean(manifest.remote.join);
    const knowledge = Boolean(manifest.remote.knowledge);
    if (knowledge && !join) {
      throw new DaorisError(
        `${MANIFEST_FILE} declares remote.knowledge without remote.join — knowledge feeds only from a `
        + 'joined repository.\n  Either "remote": { "join": true, "knowledge": true }, or neither.');
    }
    manifest.remote = { join, knowledge };
  }

  // A confirmation is a map from a core row to the pack that switches it off (D71). Anything else —
  // a list, a boolean, a row with no pack — says something this tool cannot honour, and a switch
  // that silently did nothing would leave a core row on that the repository believes is off.
  if ((parsed as Record<string, unknown>).switchedOff === null) delete manifest.switchedOff;
  if (manifest.switchedOff !== undefined) {
    const declared: unknown = manifest.switchedOff;
    const valid = typeof declared === 'object' && declared !== null && !Array.isArray(declared)
      && Object.values(declared).every((pack) => typeof pack === 'string' && pack.trim() !== '');
    if (!valid) {
      throw new DaorisError(
        `${MANIFEST_FILE} declares switchedOff as ${JSON.stringify(declared)} — it is a map from a core `
        + 'row to the pack that switches it off: { "rules/<name>.md": "<pack>" }');
    }
  }

  // Resolve here so an unknown name fails at the edge, naming what exists, rather than deeper down
  // where the message would be about a missing directory.
  manifest.harnessDescriptor = resolveHarness(manifest.harness);
  // D18 over the roots the descriptor declares and the rooms the manifest does (D117 §5.1): refused at
  // the edge, before a single path is planned, rather than corrected somewhere a reviewer never sees.
  manifest.target = checkTarget(manifest.target ?? manifest.harnessDescriptor.defaultTarget, manifest.harnessDescriptor);
  manifest.rooms = checkRooms((parsed as Record<string, unknown>).rooms, manifest.target, manifest.harnessDescriptor);
  // The development documents (D122 §2.7), refused at the same edge for the same reason: a role nobody
  // knows, a path that leaves, or a role declared twice is never half-honoured.
  manifest.documents = checkDocuments(
    (parsed as Record<string, unknown>).documents, text, manifest.target, manifest.harnessDescriptor);
  return manifest;
}

export function writeManifest(root: string, manifest: Partial<Manifest>): void {
  writeTextAtomic(join(root, MANIFEST_FILE), `${JSON.stringify(manifest, null, 2)}\n`);
}

export function readLock(root: string): Lock | null {
  const file = join(root, LOCK_FILE);
  return existsSync(file) ? parseJson<Lock>(file, readText(file)) : null;
}

/**
 * Entries are sorted by target and the shape is fixed, so the lock diffs
 * cleanly in review — a reviewer should see what moved, not a reshuffle.
 */
export function writeLock(root: string, lock: Lock): void {
  const sorted = {
    version: LOCK_VERSION,
    canonVersion: lock.canonVersion,
    source: lock.source,
    entries: [...lock.entries].sort((a, b) => a.target.localeCompare(b.target)),
    // Only when something is off (D71), so a lock that switches nothing is byte-for-byte what it was.
    ...(lock.switchedOff?.length
      ? { switchedOff: [...lock.switchedOff].sort((a, b) => a.target.localeCompare(b.target)) }
      : {}),
    // The same for the layout (D117 §5.1): absent means the older one, so a lock on it is unchanged.
    ...(lock.harness !== undefined ? { harness: lock.harness } : {}),
    ...(lock.target !== undefined ? { target: lock.target } : {}),
    ...(lock.mirrors?.length
      ? { mirrors: [...lock.mirrors].sort((a, b) => a.path.localeCompare(b.path)) }
      : {}),
    ...(lock.rooms?.length ? { rooms: [...lock.rooms].sort((a, b) => a.localeCompare(b)) } : {}),
  };
  writeTextAtomic(join(root, LOCK_FILE), `${JSON.stringify(sorted, null, 2)}\n`);
}

/** The lock is the authority on what is canonical: anything absent here is local. */
export function lockIndex<T extends { target: string }>(
  lock: { entries: readonly T[] } | null | undefined,
): Map<string, T> {
  return new Map((lock?.entries ?? []).map((entry) => [entry.target, entry] as const));
}
