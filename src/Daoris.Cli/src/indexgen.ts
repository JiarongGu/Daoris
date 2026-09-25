import type { CommandArgs, Harness, LockLike } from './types.ts';
import type { ExitCode } from './errors.ts';
import type { TierDocument } from './tierrender.ts';
import { join } from 'node:path';
import { listMarkdown, readText } from './fsx.ts';
import { stripHeader } from './document.ts';
import { lockIndex, readManifest } from './config.ts';
import { renderRoster } from './tierrender.ts';
import { DEFAULT_HARNESS, HARNESSES } from './harness.ts';

/**
 * The on-demand tiers, as they actually are on disk.
 *
 * @remarks
 * Read from disk rather than from the canon, and this is the whole reason the roster exists: a
 * repository's OWN knowledge and skills are listed beside the canonical ones, marked `_(local)_`, and
 * the canon has never seen them. An index generated from the canon would be right about the canon and
 * wrong about the repository.
 *
 * Offline and canon-free, which is what lets `check` rebuild these rows to tell whether the region
 * has gone stale (D8 — `check` sits inside build gates).
 */
export function readTier(
  { root, target, tier, entryFile, lock }:
  { root: string; target: string; tier: string; entryFile?: string; lock: LockLike | null },
): TierDocument[] {
  const locked = lockIndex(lock);
  const documents: TierDocument[] = [];

  for (const file of listMarkdown(join(root, target, tier))) {
    if (entryFile && !file.endsWith(`/${entryFile}`)) continue;
    const relative = `${tier}/${file}`;
    documents.push({
      // The roster renders a path and a name; which pack it came from is the lock's business, and
      // the only thing that shows here is whether it is the repository's own.
      file: { pack: locked.has(relative) ? 'canon' : 'local', source: relative, target: relative },
      text: stripHeader(readText(join(root, target, tier, file))),
      local: !locked.has(relative),
    });
  }

  return documents;
}

/**
 * The roster as this repository's DISK says it should be — the knowledge and skills halves only.
 *
 * 🔴 The rules half is deliberately absent. Its rows come from frontmatter that is stripped on the way
 * into the region (D59), so nothing offline can rebuild them; a canon change is `status`'s report and
 * `sync`'s job. What this catches is the case that actually happens: a local document added and the
 * region never re-synced.
 */
export function rosterFromDisk(
  { root, target, lock, harness = HARNESSES[DEFAULT_HARNESS]! }:
  { root: string; target: string; lock: LockLike | null; harness?: Harness },
): string {
  const knowledge = harness.tiers.knowledge;
  const skills = harness.tiers.skills;

  return renderRoster({
    rules: [],
    knowledge: knowledge?.dir ? readTier({ root, target, tier: knowledge.dir, lock }) : [],
    skills: skills?.dir
      ? readTier({
        root, target, tier: skills.dir, lock, ...(skills.entryFile ? { entryFile: skills.entryFile } : {}),
      })
      : [],
    version: '',
    target,
  });
}

/**
 * `daoris index` — says where the roster went, and writes nothing.
 *
 * @remarks
 * It used to write `RULES_INDEX.md`, a generated file in an always-loaded directory. The roster now
 * lives inside the region (D59), where it is loaded rather than merely present, so regenerating it is
 * regenerating the region — which needs the canon's rule bodies and is therefore `sync`'s job. The
 * command stays as a signpost for anyone whose habit is to run it.
 */
export function commandIndex({ root, write }: Pick<CommandArgs, 'root' | 'write'>): ExitCode {
  const manifest = readManifest(root);
  write('daoris: the roster is part of the doctrine region now, not a file of its own (D59).');
  write(`  It is rebuilt with the rules it sits above, so \`daoris sync\` is what regenerates it.`);
  write(`  ${manifest.target} still holds the on-demand tiers, and \`daoris check\` reports when`);
  write('  their rows have gone stale.');
  return 0;
}
