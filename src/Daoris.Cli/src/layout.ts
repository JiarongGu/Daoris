// Where a repository's doctrine sits, and the paths it may declare (LAYOUT3, D117).
//
// `docs/2026-10-01-agent-layout-design.md` §5.1 is the contract. Three questions live here because
// every command asks them: which layout the files were WRITTEN under (the lock's answer, never the
// manifest's), whether a declared path stays inside the repository (D18 over the declared roots), and
// which of the repository's own documents sit in a tier its index no longer lists.

import type { Harness, HarnessTier, Lock, Manifest } from './types.ts';
import { existsSync, readdirSync, rmdirSync, statSync } from 'node:fs';
import { dirname, join, posix, resolve, sep, win32 } from 'node:path';
import { listFiles, listMarkdown } from './fsx.ts';
import { DEFAULT_HARNESS, HARNESSES, resolveHarness } from './harness.ts';
import { DaorisError } from './errors.ts';

/**
 * The smallest limit an agent puts on a repository's instruction files: codex reads 32,768 bytes of
 * the project chain, root first, and cuts the file where the budget runs out (LAYOUT2,
 * `docs/2026-10-01-entry-point-evidence.md` §2). dsh caps the chain at 65,536 and Claude Code skips a
 * file over 4 MiB, so this is the one a root `AGENTS.md` meets first. `check` reports against it and
 * never fails on it (D54): a size is a judgement, and the tail it would lose is the region's last rule.
 */
export const INSTRUCTION_LIMIT = 32768;

/** The layout every lock written before D117 was written under. */
const OLDER = HARNESSES[DEFAULT_HARNESS]!;

/** A declared path as the manifest and the lock spell it: forward slashes, no `./`, no trailing slash. */
export function declared(path: string): string {
  return path.replace(/\\/g, '/').replace(/^(\.\/)+/, '').replace(/\/+$/, '');
}

/** Whether a declared path leaves the repository: absolute on either platform, or climbing out. */
export function escapes(path: string): boolean {
  if (posix.isAbsolute(path) || win32.isAbsolute(path) || /^[A-Za-z]:/.test(path)) return true;
  const normal = posix.normalize(path);
  return normal === '..' || normal.startsWith('../');
}

/** Whether `child` is `parent` or sits below it, both declared paths. */
export const within = (child: string, parent: string): boolean =>
  child === parent || child.startsWith(`${parent}/`);

const overlap = (a: string, b: string): boolean => within(a, b) || within(b, a);

/**
 * The manifest's target, checked against what the descriptor declares (D117 §5.1, D18).
 *
 * @remarks
 * A target is a folder inside the repository; one that leaves it would put every write somewhere a
 * reviewer never looks. And under a descriptor with a mirror, the mirrored tier may not land on the
 * mirror's own root: the source and its copy would be one file, and every `sync` would overwrite the
 * repository's skill with a copy of itself.
 */
export function checkTarget(target: string, harness: Harness): string {
  const normal = declared(target);
  if (escapes(normal)) {
    throw new DaorisError(
      `daoris.json's target '${target}' is not inside the repository — it is the folder daoris writes `
      + 'its on-demand tiers into, and it must be one inside the repository');
  }
  const mirror = harness.mirror;
  const tier = mirror ? harness.tiers[mirror.tier] : undefined;
  if (mirror && tier?.dir) {
    const lands = normal === '' ? tier.dir : `${normal}/${tier.dir}`;
    if (overlap(lands, mirror.root) || overlap(normal, mirror.root)) {
      throw new DaorisError(
        `daoris.json's target '${target}' puts ${tier.dir}/ at ${lands}, on ${mirror.root}, where the mirror `
        + `for ${mirror.reader} is written. The source and its copy cannot share a folder: the ${harness.id} `
        + `layout's target is ${harness.defaultTarget}`);
    }
  }
  return normal;
}

/**
 * The manifest's rooms, checked (D117 §2.2, §5.1): a list of folders of the repository's own, never its
 * root, a tier's folder or a mirror's. Refused rather than corrected, as D18 refuses a lock entry: a
 * room that escapes is a pointer written somewhere nobody asked.
 */
export function checkRooms(raw: unknown, target: string, harness: Harness): string[] {
  if (raw === undefined || raw === null) return [];
  if (!Array.isArray(raw) || raw.some((room) => typeof room !== 'string')) {
    throw new DaorisError(
      `daoris.json declares rooms as ${JSON.stringify(raw)} — it is a list of folders with an AGENTS.md `
      + 'of their own: [ "src/app", "docs" ]');
  }
  const rooms: string[] = [];
  for (const room of raw as string[]) {
    const normal = declared(room);
    const refuse = (why: string) => {
      throw new DaorisError(`daoris.json's room '${room}' ${why}`);
    };
    if (escapes(normal)) refuse('leaves the repository — a room is a folder inside it');
    if (normal === '' || normal === '.') {
      refuse("is the repository's root — the root's AGENTS.md is the repository's own, and a room is a folder below it");
    }
    if (within(normal, target)) refuse(`sits inside ${target}, where daoris writes the on-demand tiers`);
    if (harness.mirror && within(normal, harness.mirror.root)) {
      refuse(`sits inside ${harness.mirror.root}, where daoris writes the mirror for ${harness.mirror.reader}`);
    }
    if (rooms.includes(normal)) refuse('is declared twice');
    rooms.push(normal);
  }
  return rooms;
}

/**
 * The layout a lock's files were written under (D117 §5.1).
 *
 * 🔴 **The lock, not the manifest, says where the files are.** Between a manifest's flip and the sync
 * that moves them, the manifest names the new root and the files are still at the old. Absent fields
 * mean `claude-code` under `.claude`, the only layout written before — except a lock with neither field
 * beside a manifest still on `claude-code`, which an older build wrote at the manifest's own target.
 *
 * @throws DaorisError when the lock's target leaves the repository (D18): the lock is generated, the
 * file nobody reads closely, so a crafted root is refused rather than written into.
 */
export function lockLayout(
  _root: string, lock: Lock | null, manifest: Manifest,
): { harness: Harness; target: string } {
  if (!lock) return { harness: manifest.harnessDescriptor, target: manifest.target };

  const harness = lock.harness === undefined ? OLDER : resolveHarness(lock.harness);
  const target = lock.target !== undefined
    ? lock.target
    : lock.harness === undefined && manifest.harnessDescriptor.id === OLDER.id
      ? manifest.target
      : harness.defaultTarget;

  const normal = declared(target);
  if (escapes(normal)) {
    throw new DaorisError(
      `daoris.lock says its files are under '${target}', outside the repository — refusing to touch it.\n`
      + '  a lock whose target escapes the repository is corrupt or has been tampered with');
  }
  return { harness, target: normal };
}

/** Whether a lock records a layout other than the older one, and so carries the fields (additive, as D71's). */
export function layoutFields(harness: Harness, target: string): { harness: string; target: string } | null {
  return harness.id === OLDER.id && target === OLDER.defaultTarget ? null : { harness: harness.id, target };
}

/**
 * The folder a path must resolve inside, or a refusal (D18). Every write, delete, mirror and pointer
 * goes through here, against the root that declared it.
 */
export function contained(root: string, base: string, rel: string): string {
  const inside = resolve(root, base);
  const full = resolve(root, rel);
  if (full !== inside && !full.startsWith(inside + sep)) {
    throw new DaorisError(
      `'${rel}' resolves outside ${base || '.'}/ — refusing to touch it.\n`
      + '  daoris only ever writes inside the roots it declares; a lock entry that escapes one\n'
      + '  means daoris.lock is corrupt or has been tampered with');
  }
  return full;
}

/** A file, and not a folder or nothing. */
export function isFile(abs: string): boolean {
  try {
    return statSync(abs).isFile();
  } catch {
    return false;
  }
}

/**
 * Remove the folders a delete emptied, from the file's own up, stopping at the first that still holds
 * something and never at or above the repository root. Only ever an empty folder (D117 §5.2).
 */
export function pruneEmpty(root: string, deleted: readonly string[]): void {
  const top = resolve(root);
  const seen = new Set<string>();
  for (const file of deleted) {
    let dir = dirname(file);
    while (dir !== top && dir.startsWith(top + sep) && !seen.has(dir)) {
      if (!existsSync(dir) || readdirSync(dir).length) break;
      rmdirSync(dir);
      seen.add(dir);
      dir = dirname(dir);
    }
  }
}

/** One of the repository's own documents in a tier: a file, or a skill's whole folder. */
interface OwnUnit {
  /** Relative to the repository: the document, or the skill's folder. */
  path: string;
  /** Relative to the tier's folder, so the same unit can be found under another root. */
  name: string;
}

/**
 * The repository's own documents under one tier's folder, as its index would list them: a knowledge
 * document, or a skill's folder when its entry file is the repository's own. A file of the
 * repository's own inside a canonical skill's folder is listed alone, since the folder is not its own.
 */
export function ownInTier(
  root: string, base: string, tierName: string, tier: HarnessTier,
  owned: (target: string) => boolean,
): OwnUnit[] {
  if (!tier.dir) return [];
  const folder = base === '' ? tier.dir : `${base}/${tier.dir}`;
  const units: OwnUnit[] = [];

  if (!tier.entryFile) {
    for (const file of listMarkdown(join(root, folder))) {
      if (!owned(`${tierName}/${file}`)) units.push({ path: `${folder}/${file}`, name: file });
    }
    return units;
  }

  const byUnit = new Map<string, string[]>();
  for (const file of listFiles(join(root, folder))) {
    const [unit, ...rest] = file.split('/');
    if (!unit || !rest.length) continue;
    byUnit.set(unit, [...(byUnit.get(unit) ?? []), file]);
  }
  for (const [unit, files] of byUnit) {
    const entry = `${unit}/${tier.entryFile}`;
    if (!files.includes(entry)) continue;
    if (!owned(`${tierName}/${entry}`)) {
      units.push({ path: `${folder}/${unit}`, name: unit });
      continue;
    }
    for (const file of files) {
      if (!owned(`${tierName}/${file}`)) units.push({ path: `${folder}/${file}`, name: file });
    }
  }
  return units;
}

/** The on-demand tiers a descriptor keeps as folders, by canon name. */
export const folderTiers = (harness: Harness): [string, HarnessTier][] =>
  Object.entries(harness.tiers).filter(([, tier]) => tier.dir !== undefined);

/**
 * What a move leaves behind (D117 §5.4, the repository's own documents): each of its own documents in
 * an old on-demand tier refuses the move, with the `git mv` that moves it — left there, the index stops
 * listing it and nothing else says so. The same document under both roots refuses too, since which copy
 * is meant is the repository's call. An old `rules/` file is only reported: Claude Code still reads it,
 * and its text could go in the repository's own part of `AGENTS.md`.
 */
export function planLeftBehind(
  { root, from, to, was, now, owned, mirrors }:
  {
    root: string; from: string; to: string; was: Harness; now: Harness;
    owned: (target: string) => boolean; mirrors: ReadonlySet<string>;
  },
): { leftBehind: { path: string; move: string }[]; bothRoots: { old: string; neu: string }[]; keptRules: string[] } {
  const leftBehind: { path: string; move: string }[] = [];
  const bothRoots: { old: string; neu: string }[] = [];

  for (const [name, tier] of folderTiers(was)) {
    const into = now.tiers[name];
    if (!into?.dir) continue;
    for (const unit of ownInTier(root, from, name, tier, owned)) {
      // A mirror the lock records is Daoris's own file, wherever the old tier happens to sit.
      if (mirrors.has(unit.path) || [...mirrors].some((path) => within(path, unit.path))) continue;
      const neu = `${to === '' ? '' : `${to}/`}${into.dir}/${unit.name}`;
      if (existsSync(join(root, neu))) bothRoots.push({ old: unit.path, neu });
      else leftBehind.push({ path: unit.path, move: `git mv ${unit.path} ${neu}` });
    }
  }

  // Before D59 the always-loaded tier was a folder, so a repository may still keep its own rules there.
  const keptRules = listMarkdown(join(root, from, 'rules'))
    .filter((file) => file !== 'RULES_INDEX.md' && !owned(`rules/${file}`))
    .map((file) => `${from}/rules/${file}`);

  return { leftBehind, bothRoots, keptRules };
}

/**
 * The repository's own documents a descriptor's index does not list, for `check` to report and never
 * fail on (D117 §5.2): a document in the root the older layout used, with the move that fixes it; and a
 * skill of its own under the mirror root, which only the mirror's harness reads.
 */
export function unlistedDocuments(
  { root, harness, target, mirrors }:
  { root: string; harness: Harness; target: string; mirrors: ReadonlySet<string> },
): { unlisted: { path: string; move: string }[]; readAlone: string[] } {
  const unlisted: { path: string; move: string }[] = [];
  const readAlone: string[] = [];
  const mirror = harness.mirror;
  for (const doc of formerDocuments({ root, harness, target, mirrors })) {
    // Under the mirror root, a skill of the repository's own is a choice (D117 §2.3): read by one agent.
    if (mirror && within(doc.path, mirror.root)) readAlone.push(doc.path);
    else unlisted.push(doc);
  }
  return { unlisted, readAlone };
}

/**
 * The repository's own documents in the root the older layout kept its on-demand tiers in, each with
 * the `git mv` that moves it under the descriptor's target — what `init` names (D117 §5.2), since the
 * index lists the target only. A mirror the lock records is Daoris's, and is never one of them.
 */
export function formerDocuments(
  { root, harness, target, mirrors = new Set<string>() }:
  { root: string; harness: Harness; target: string; mirrors?: ReadonlySet<string> },
): { path: string; move: string }[] {
  if (harness.formerly === undefined) return [];
  const docs: { path: string; move: string }[] = [];
  for (const [name, tier] of folderTiers(resolveHarness(DEFAULT_HARNESS))) {
    const into = harness.tiers[name];
    if (!into?.dir) continue;
    for (const unit of ownInTier(root, harness.formerly, name, tier, () => false)) {
      if (mirrors.has(unit.path) || [...mirrors].some((path) => within(path, unit.path))) continue;
      docs.push({ path: unit.path, move: `git mv ${unit.path} ${target}/${into.dir}/${unit.name}` });
    }
  }
  return docs;
}
