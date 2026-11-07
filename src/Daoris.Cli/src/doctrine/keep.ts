import type { Canon, CanonFile, Harness, KeepState, Manifest } from './records.ts';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { digestBytes, readText } from '../platform/fsx.ts';
import { findRegion } from './region.ts';
import { tierRuleBody } from './tierrender.ts';

/** The region a target lives in, for an always-loaded tier that is a span rather than a folder; null for a file. */
export function regionFor(harness: Harness, target: string): { file: string; name: string } | null {
  for (const tier of Object.values(harness.tiers)) {
    if (tier.region && tier.dir === undefined && target.startsWith(`${tier.region.name}/`)) return tier.region;
  }
  return null;
}

/** Whether this repository holds a canonical file: its body in the region, or a file at its target under `at`. */
export function holds(root: string, harness: Harness, at: string, file: CanonFile): boolean {
  const region = regionFor(harness, file.target);
  if (!region) return existsSync(join(root, at, file.target));
  const abs = join(root, region.file);
  const held = existsSync(abs) ? findRegion(readText(abs), region.name) : null;
  return held?.kind === 'present' && tierRuleBody(held.body, `${file.pack}/${file.source}`) !== null;
}

/**
 * The digest a keep records: the canon's file as it ships, hashed as `canonDigest` hashes each file, so any change
 * to that one file, its frontmatter included, reopens the keep and nothing else does.
 */
export function canonFileDigest(canon: Pick<Canon, 'root'>, file: Pick<CanonFile, 'source'>): string {
  return digestBytes(readFileSync(join(canon.root, file.source)));
}

/**
 * Each of the manifest's keeps against what the canon selects here. A keep the selection no longer ships is dropped: the
 * canon retired the file or a switch took it out, so there is no canon text left to keep one's own against, and the ordinary rows
 * apply to the file as to any other.
 *
 * @param present Whether this repository holds the file: a span's body in its region, or a file at its target.
 */
export function keepStates(
  manifest: Pick<Manifest, 'kept'>, canon: Pick<Canon, 'root'>, selected: readonly CanonFile[],
  present: (file: CanonFile) => boolean,
): { kept: KeepState[]; dropped: string[] } {
  const byTarget = new Map(selected.map((file) => [file.target, file]));
  const kept: KeepState[] = [];
  const dropped: string[] = [];
  for (const [target, keep] of Object.entries(manifest.kept ?? {}).sort(([a], [b]) => a.localeCompare(b))) {
    const file = byTarget.get(target);
    if (!file) {
      dropped.push(target);
      continue;
    }
    const now = canonFileDigest(canon, file);
    kept.push({
      target, reason: keep.reason, canon: keep.canon, now,
      reopened: now !== keep.canon,
      absentCore: file.pack === 'core' && !present(file),
    });
  }
  return { kept, dropped };
}
