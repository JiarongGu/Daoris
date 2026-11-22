import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { compareOrdinal, readText, writeTextAtomic } from '../platform/fsx.ts';
import { DaorisError } from '../platform/errors.ts';
import { DEFAULT_HARNESS, resolveHarness } from '../layout/harness.ts';
import { checkRooms, checkTarget } from '../layout/layout.ts';
import { checkDocuments } from '../layout/documents.ts';
import type { Lock, Manifest } from './records.ts';

export const MANIFEST_FILE = 'daoris.json';
const LOCK_FILE = 'daoris.lock';
const LOCK_VERSION = 1;

// `harness` names which agent layout to generate. It defaults rather than being required, because a
// manifest written before harnesses existed must keep working: absent is `claude-code`, the older
// layout, and `agents` is chosen by name.
/**
 * `coreBudget.words` is what a repository is willing to pay, in always-loaded doctrine, on every task: the
 * region's words, which `check` measures. The whole root instruction file has its own ceiling, the brief's.
 *
 * The default leaves the core and a few packs' rules inside, so it fires on what matters: a repository's
 * own always-loaded choice getting fat. It reports; a repository that declares `"fails": true` holds it.
 */
export const DEFAULT_CORE_BUDGET_WORDS = 4500;

const DEFAULTS: Pick<Manifest, 'packs' | 'harness' | 'coreBudget'> & { target: string | null } =
  { packs: [], harness: DEFAULT_HARNESS, target: null, coreBudget: { words: DEFAULT_CORE_BUDGET_WORDS, fails: false } };

/**
 * `coreBudget` as `check` reads it, or a refusal. The manifest is written by hand, so the byte ceiling it replaced is refused with
 * the line that replaces it: a ceiling silently ignored is one nobody knows has stopped applying.
 */
function checkCoreBudget(parsed: Record<string, unknown>): Manifest['coreBudget'] {
  if ('coreBudgetBytes' in parsed) {
    throw new DaorisError(`${MANIFEST_FILE} has coreBudgetBytes, which is retired: the always-loaded doctrine's ceiling `
      + `is in words — replace it with "coreBudget": { "words": ${DEFAULT_CORE_BUDGET_WORDS} }`);
  }
  const value = parsed.coreBudget;
  if (value === undefined) return DEFAULTS.coreBudget;
  const words = (value as Record<string, unknown> | null)?.words;
  const fails = (value as Record<string, unknown> | null)?.fails;
  if (typeof value !== 'object' || value === null || Array.isArray(value)
    || Object.keys(value).some((key) => key !== 'words' && key !== 'fails')
    || !(typeof words === 'number' && Number.isInteger(words) && words > 0)
    || (fails !== undefined && typeof fails !== 'boolean')) {
    throw new DaorisError(`${MANIFEST_FILE}'s coreBudget is ${JSON.stringify(value)} — it is { "words": <a whole number above `
      + 'zero>, "fails": true or false }');
  }
  return { words, fails: fails === true };
}

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
  const manifest = { ...DEFAULTS, ...parsed, coreBudget: checkCoreBudget(parsed as Record<string, unknown>) } as Manifest;
  if (!manifest.source) throw new DaorisError(`${MANIFEST_FILE} has no 'source'`);

  // Knowledge feeds only from a joined repository: a manifest saying "share my knowledge but
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

  // A confirmation is a map from a core row to the pack that switches it off. Anything else —
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

  // A keep is a reason and the canon file's digest it is judged against. One without either could never be
  // reopened by the canon or read by the next person, so it is refused here rather than honoured half.
  if ((parsed as Record<string, unknown>).kept === null) delete manifest.kept;
  if (manifest.kept !== undefined) {
    const declared: unknown = manifest.kept;
    const isKeep = (keep: unknown): boolean => {
      const { reason, canon } = (typeof keep === 'object' && keep !== null ? keep : {}) as Record<string, unknown>;
      return typeof reason === 'string' && reason.trim() !== '' && typeof canon === 'string' && /^[0-9a-f]{64}$/.test(canon);
    };
    const valid = typeof declared === 'object' && declared !== null && !Array.isArray(declared)
      && Object.values(declared).every(isKeep);
    if (!valid) {
      throw new DaorisError(
        `${MANIFEST_FILE} declares kept as ${JSON.stringify(declared)} — it is a map from a canonical file to why this `
        + "repository keeps its own text and the canon file's digest it was judged against: "
        + '{ "rules/<name>.md": { "reason": "<why>", "canon": "<sha256>" } }, as \'daoris reconcile\' gives it');
    }
  }

  // Resolve here so an unknown name fails at the edge, naming what exists, rather than deeper down
  // where the message would be about a missing directory.
  manifest.harnessDescriptor = resolveHarness(manifest.harness);
  // Containment over the roots the descriptor declares and the rooms the manifest does: refused at
  // the edge, before a single path is planned, rather than corrected somewhere a reviewer never sees.
  manifest.target = checkTarget(manifest.target ?? manifest.harnessDescriptor.defaultTarget, manifest.harnessDescriptor);
  manifest.rooms = checkRooms((parsed as Record<string, unknown>).rooms, manifest.target, manifest.harnessDescriptor);
  // The development documents, refused at the same edge for the same reason: a role nobody
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
  if (!existsSync(file)) return null;
  const lock = parseJson<Partial<Lock> | null>(file, readText(file));
  if (typeof lock !== 'object' || lock === null || Array.isArray(lock)) {
    throw new DaorisError(`${LOCK_FILE} is not a lock — it is not a JSON object`);
  }
  // Read as version 1, a lock of another version would be checked and rewritten by rules it does not follow.
  if (lock.version !== LOCK_VERSION) {
    const said = lock.version === undefined ? 'no version' : `version ${JSON.stringify(lock.version)}`;
    throw new DaorisError(
      `${LOCK_FILE} has ${said}, and this daoris reads only version ${LOCK_VERSION} — nothing was changed.\n`
      + `  Run the daoris that wrote it (its manifest's 'source' names it).`,
      1);
  }
  if (!Array.isArray(lock.entries)) {
    throw new DaorisError(`${LOCK_FILE} has no 'entries' list — it is damaged; 'daoris sync' writes it again`);
  }
  return lock as Lock;
}

/**
 * The canon digest a lock names, or a refusal: a lock is the repository's own file, so one with no digest is in front of this
 * daoris, and comparing against nothing would crash. `sync` writes the digest, so it never asks this.
 */
export function lockedDigest(lock: Lock): string {
  const digest = (lock as Partial<Lock>).canonDigest;
  if (typeof digest !== 'string' || digest.length === 0) {
    throw new DaorisError(`${LOCK_FILE}: the lock predates its digest: run daoris sync`, 1);
  }
  return digest;
}

/**
 * Entries are sorted by target and the shape is fixed, so the lock diffs
 * cleanly in review — a reviewer should see what moved, not a reshuffle.
 */
export function writeLock(root: string, lock: Lock): void {
  const sorted = {
    version: LOCK_VERSION,
    canonVersion: lock.canonVersion,
    canonDigest: lock.canonDigest,
    source: lock.source,
    entries: [...lock.entries].sort((a, b) => compareOrdinal(a.target, b.target)),
    // Only when something is off, so a lock that switches nothing is byte-for-byte unchanged.
    ...(lock.switchedOff?.length
      ? { switchedOff: [...lock.switchedOff].sort((a, b) => compareOrdinal(a.target, b.target)) }
      : {}),
    // The same for the layout: absent means the older one, so a lock on it is unchanged.
    ...(lock.harness !== undefined ? { harness: lock.harness } : {}),
    ...(lock.target !== undefined ? { target: lock.target } : {}),
    index: lock.index,
    ...(lock.mirrors?.length
      ? { mirrors: [...lock.mirrors].sort((a, b) => compareOrdinal(a.path, b.path)) }
      : {}),
  };
  writeTextAtomic(join(root, LOCK_FILE), `${JSON.stringify(sorted, null, 2)}\n`);
}

/** The lock is the authority on what is canonical: anything absent here is local. */
export function lockIndex<T extends { target: string }>(
  lock: { entries: readonly T[] } | null | undefined,
): Map<string, T> {
  return new Map((lock?.entries ?? []).map((entry) => [entry.target, entry] as const));
}
