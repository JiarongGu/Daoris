import type { Canon, CanonFile, Harness, Survey } from './records.ts';
import { existsSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { readText } from '../platform/fsx.ts';
import { parseFrontmatter } from './document.ts';
import { findRegion } from './region.ts';
import { tierRuleBody } from './tierrender.ts';

const bytesOf = (text: string): number => Buffer.byteLength(text, 'utf8');

/**
 * Bytes of always-loaded context after adopting — the number that is paid every session.
 *
 * Where the rules tier is a span, its body lives in the region, so that is measured (as `inspect` measures it, beside any
 * rule file the repository keeps of its own) and a selected rule the region already holds adds only what its text changes
 * by, never its whole size again.
 */
export function projectBudget(
  root: string, target: string, harness: Harness, canon: Canon, selected: readonly CanonFile[],
  existing: Survey, collisions: readonly string[],
): { current: number; projected: number } {
  const region = Object.values(harness.tiers).find((tier) => tier.region)?.region ?? null;
  let held: string | null = null;
  if (region && existsSync(join(root, region.file))) {
    const found = findRegion(readText(join(root, region.file)), region.name);
    held = found.kind === 'present' ? found.body : null;
  }

  const current = existing.rules.reduce((sum, f) => sum + f.bytes, 0) + (held === null ? 0 : bytesOf(held));
  const collided = new Set(collisions);

  let added = 0;
  for (const file of selected) {
    if (!file.target.startsWith('rules/')) continue;
    const text = readText(join(canon.root, file.source));

    const inRegion = held === null ? null : tierRuleBody(held, `${file.pack}/${file.source}`);
    if (inRegion !== null) {
      const { meta, body } = parseFrontmatter(text, []);
      added += bytesOf((meta ? body : text).trim()) - bytesOf(inRegion);
      continue;
    }

    const abs = join(root, target, file.target);
    // A collision replaces rather than adds; an existing identical file changes nothing.
    if (existsSync(abs) && !collided.has(file.target)) continue;
    added += bytesOf(text);
    if (existsSync(abs)) added -= statSync(abs).size;
  }

  return { current, projected: current + added };
}
