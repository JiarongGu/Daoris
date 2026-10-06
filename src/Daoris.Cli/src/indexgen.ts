import type { CommandArgs, DeclaredDocument, Harness, LockLike } from './types.ts';
import type { ExitCode } from './errors.ts';
import type { IndexInput, TierDocument, TierInput } from './tierrender.ts';
import { join } from 'node:path';
import { listMarkdown, readText } from './fsx.ts';
import { stripHeader } from './document.ts';
import { lockIndex, readManifest } from './config.ts';
import { indexPath, renderIndex, renderRoster } from './tierrender.ts';
import { DEFAULT_HARNESS, HARNESSES } from './harness.ts';
import { roomRows } from './rooms.ts';
import { declaredPaths, jobOf } from './documents.ts';

/**
 * What a sync is about to do to the tiers, by canon target: the bytes it writes and what it deletes.
 * Laid over the disk, it is the tier as the sync will leave it, which is what decides whether an index
 * already on disk is what would be written (D128 §2.4) before anything is.
 */
export interface TierOverlay {
  writes: ReadonlyMap<string, string>;
  deletes: ReadonlySet<string>;
}

/**
 * The on-demand tiers, as they actually are on disk — or as a sync will leave them, with `overlay`.
 *
 * @remarks
 * Read from disk rather than from the canon, and this is the whole reason the index exists: a
 * repository's OWN knowledge and skills are listed beside the canonical ones, marked `_(local)_`, and
 * the canon has never seen them. An index generated from the canon would be right about the canon and
 * wrong about the repository.
 *
 * Offline and canon-free, which is what lets `check` rebuild the index to tell whether it has gone
 * stale (D8 — `check` sits inside build gates). With an overlay, what the sync writes is the canon's
 * and everything else is the repository's own, which is what the lock will say once it is written.
 */
export function readTier(
  { root, target, tier, entryFile, lock, overlay }:
  { root: string; target: string; tier: string; entryFile?: string; lock: LockLike | null; overlay?: TierOverlay },
): TierDocument[] {
  const owned = overlay ? new Set(overlay.writes.keys()) : new Set(lockIndex(lock).keys());
  const files = new Set(listMarkdown(join(root, target, tier)));
  const prefix = `${tier}/`;
  for (const deleted of overlay?.deletes ?? []) if (deleted.startsWith(prefix)) files.delete(deleted.slice(prefix.length));
  for (const written of overlay?.writes.keys() ?? []) {
    if (written.startsWith(prefix) && written.endsWith('.md')) files.add(written.slice(prefix.length));
  }

  const documents: TierDocument[] = [];
  // Sorted as `listMarkdown` sorts, so a planned index and one rebuilt from the disk agree byte for byte.
  for (const file of [...files].sort()) {
    if (entryFile && !file.endsWith(`/${entryFile}`)) continue;
    const relative = `${tier}/${file}`;
    documents.push({
      // The index renders a path; which pack it came from is the lock's business, and the only thing
      // that shows here is whether it is the repository's own.
      file: { pack: owned.has(relative) ? 'canon' : 'local', source: relative, target: relative },
      text: stripHeader(overlay?.writes.get(relative) ?? readText(join(root, target, tier, file))),
      local: !owned.has(relative),
    });
  }

  return documents;
}

/** The knowledge and skills under `target`, read as `readTier` reads them, for `renderIndex`. */
export function indexInput(
  { root, target, lock, harness = HARNESSES[DEFAULT_HARNESS]!, overlay }:
  { root: string; target: string; lock: LockLike | null; harness?: Harness; overlay?: TierOverlay },
): IndexInput {
  const knowledge = harness.tiers.knowledge;
  const skills = harness.tiers.skills;
  return {
    knowledge: knowledge?.dir ? readTier({ root, target, tier: knowledge.dir, lock, ...(overlay ? { overlay } : {}) }) : [],
    skills: skills?.dir
      ? readTier({
        root, target, tier: skills.dir, lock,
        ...(skills.entryFile ? { entryFile: skills.entryFile } : {}),
        ...(overlay ? { overlay } : {}),
      })
      : [],
    target,
  };
}

/**
 * `<target>/INDEX.md` as this repository's DISK says it should be (D128 §2.4): what `check` compares the
 * file with. What catches the case that actually happens — a document added and never synced. `check`
 * renders it from `indexInput` itself, since it also counts the documents without frontmatter there.
 */
export function indexFromDisk(
  args: { root: string; target: string; lock: LockLike | null; harness?: Harness },
): string {
  return renderIndex(indexInput(args));
}

/**
 * The region's roster as this repository's DISK and manifest say it should be: the pointer, the mirror
 * sentence, the rooms and where the records are.
 *
 * 🔴 The rules half is deliberately absent. Its rows come from frontmatter that is stripped on the way
 * into the region (D59), so nothing offline can rebuild them; a canon change is `status`'s report and
 * `sync`'s job.
 */
export function rosterFromDisk(
  { root, target, harness = HARNESSES[DEFAULT_HARNESS]!, rooms = [], documents = [] }:
  {
    root: string; target: string; harness?: Harness; rooms?: readonly string[];
    documents?: readonly DeclaredDocument[];
  },
): string {
  return renderRoster({
    rules: [],
    version: '',
    target,
    ...rosterExtras(root, harness, target, rooms, documents),
  });
}

/**
 * What the roster says beyond the tiers: where the skills live and what mirrors them, and each declared
 * room with its heading (D117 §5.3); and where each declared record is (D122 §2.7). One function for
 * `sync`'s render and `check`'s rebuild, so the two cannot disagree about a region neither changed.
 */
export function rosterExtras(
  root: string, harness: Harness, target: string, rooms: readonly string[],
  documents: readonly DeclaredDocument[] = [],
): Pick<TierInput, 'mirror' | 'rooms' | 'documents'> {
  const mirror = harness.mirror;
  const dir = mirror ? harness.tiers[mirror.tier]?.dir : undefined;
  const rows = declaredPaths(documents).map((row) => ({ ...row, job: jobOf(row.role) }));
  return {
    ...(mirror && dir ? { mirror: { source: `${target}/${dir}`, root: mirror.root } } : {}),
    ...(rooms.length ? { rooms: roomRows(root, rooms, harness) } : {}),
    ...(rows.length ? { documents: rows } : {}),
  };
}

/**
 * `daoris index` — says where the roster is, and writes nothing.
 *
 * @remarks
 * It used to write `RULES_INDEX.md`, a generated file in an always-loaded directory. The rules table
 * lives inside the region (D59), where it is loaded rather than merely present, and since WSSETUP14a
 * the knowledge and skills are listed in `<target>/INDEX.md` (D128 §2.5). Both are `sync`'s to write:
 * the region needs the canon's rule bodies, and the two are rendered by one run. The command stays as
 * a signpost for anyone whose habit is to run it.
 */
export function commandIndex({ root, write }: Pick<CommandArgs, 'root' | 'write'>): ExitCode {
  const manifest = readManifest(root);
  write('daoris: the roster is two things now, and `daoris sync` writes both (D59, D128):');
  write('  the rules, in the doctrine region of AGENTS.md, which points at the index;');
  write(`  the knowledge and skills, in ${indexPath(manifest.target)}, generated from the files under ${manifest.target || '.'}/.`);
  write('  `daoris check` reports when either has gone stale.');
  return 0;
}
