import type { Canon, CanonFile, Harness, Survey } from './records.ts';
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { readText } from '../platform/fsx.ts';
import { parseFrontmatter } from './document.ts';
import { findRegion } from './region.ts';
import { tierRuleBody } from './tierrender.ts';
import { wordCount } from '../layout/documents.ts';

/**
 * Words of always-loaded doctrine after adopting — the number that is paid every session, in the unit `check` holds
 * `coreBudget` to.
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

  const wordsAt = (path: string): number => (existsSync(path) ? wordCount(readText(path)) : 0);
  const current = existing.rules.reduce((sum, f) => sum + wordsAt(join(root, target, f.target)), 0)
    + (held === null ? 0 : wordCount(held));
  const collided = new Set(collisions);

  let added = 0;
  for (const file of selected) {
    if (!file.target.startsWith('rules/')) continue;
    const text = readText(join(canon.root, file.source));

    const inRegion = held === null ? null : tierRuleBody(held, `${file.pack}/${file.source}`);
    if (inRegion !== null) {
      const { meta, body } = parseFrontmatter(text, []);
      added += wordCount((meta ? body : text).trim()) - wordCount(inRegion);
      continue;
    }

    const abs = join(root, target, file.target);
    // A collision replaces rather than adds; an existing identical file changes nothing.
    if (existsSync(abs) && !collided.has(file.target)) continue;
    added += wordCount(text) - wordsAt(abs);
  }

  return { current, projected: current + added };
}
