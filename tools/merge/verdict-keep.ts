/**
 * Which verdicts of the gate store (`local/gate-verdicts.json`) are kept: none past its use, which the store's reader leaves out
 * and so its next write drops (VERDICTSCAN1); and of each gate a count of the newest, the scoped run's apart from every other
 * kind's. A judgement compares each verdict it reads with the checkout's tree, and a verdict on a tree a month behind main
 * differs from main by nearly every path: it stands for no gate and costs the judgement a diff of thousands of paths.
 */

type Kept = { gate: string; verdict: string; at?: string; kind?: string | null };

/**
 * How many verdicts the store keeps of each gate, apart for the scoped run's (`kind` `scoped`) and every other kind's, so a
 * working day of scoped verdicts never evicts a night's or a stage's.
 */
export const VERDICTS_KEPT_PER_GATE = 40;

/**
 * How long a verdict is of use once it is no longer its gate's newest, nor its newest pass: a week. Main moves by thousands of
 * paths in a week (on 2026-11-18 every tree of the store older than ten days differed from main's by over 10,000), so an older
 * tree stands for no gate a change can reach.
 */
export const VERDICTS_KEPT_DAYS = 7;

const groupOf = (entry: Kept): string => `${entry.gate}\u0000${entry.kind === 'scoped' ? 'scoped' : 'background'}`;

/** The verdicts kept: of each gate, the newest `keep` of the scoped run's and the newest `keep` of the rest, in their order. */
export function keptVerdicts<V extends Kept>(verdicts: V[], keep = VERDICTS_KEPT_PER_GATE): V[] {
  const counts = new Map<string, number>();
  const kept: V[] = [];
  for (let i = verdicts.length - 1; i >= 0; i -= 1) {
    const entry = verdicts[i]!;
    const seen = counts.get(groupOf(entry)) ?? 0;
    if (seen >= keep) continue;
    counts.set(groupOf(entry), seen + 1);
    kept.push(entry);
  }
  return kept.reverse();
}

/**
 * The verdicts still of use at `now`, in their order: every one given within `days`, and older ones only where it is its gate's
 * newest verdict, or its newest pass, of the scoped run's or of the rest (the newest by its time, the later in the store of
 * equal times, as the stage reads them). A stage may count a gate's newest pass however old, and says its newest verdict when
 * none counts; an older verdict counts only where a newer one of its gate went stale and it did not, which a week of main's
 * changes leaves no tree to do, so the gate runs again.
 */
export function inUse<V extends Kept>(verdicts: V[], now = Date.now(), days = VERDICTS_KEPT_DAYS): V[] {
  const newest = new Map<string, V>();
  const newestPass = new Map<string, V>();
  const later = (entry: V, than: V | undefined): boolean => !than || String(entry.at) >= String(than.at);
  for (const entry of verdicts) {
    const group = groupOf(entry);
    if (later(entry, newest.get(group))) newest.set(group, entry);
    if ((entry.verdict === 'PASS' || entry.verdict === 'FLAKE') && later(entry, newestPass.get(group))) newestPass.set(group, entry);
  }
  const bound = days * 86_400_000;
  return verdicts.filter((entry) => now - Date.parse(entry.at ?? '') <= bound
    || newest.get(groupOf(entry)) === entry || newestPass.get(groupOf(entry)) === entry);
}
