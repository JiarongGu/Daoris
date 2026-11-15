import type { Attention } from './AttentionRow';

const ASIDE_AT_ONCE = 3;

/** The kinds three or more rows hold, with their counts: what one press may set aside, never a press's own card. */
export function repeatedKinds(items: readonly Attention[]): { kind: Attention['kind']; count: number }[] {
  const counts = new Map<Attention['kind'], number>();
  for (const item of items) if (item.kind !== 'confirmation') counts.set(item.kind, (counts.get(item.kind) ?? 0) + 1);
  return [...counts].filter(([, count]) => count >= ASIDE_AT_ONCE).map(([kind, count]) => ({ kind, count }));
}
