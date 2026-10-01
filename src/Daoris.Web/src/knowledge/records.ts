import { type Convergence, type Entry, notFound } from '../api';
import { sentence } from '../format';

// What Search's and Convergence's lists remember and read (FRAME1f, D118 §2, §3f): their filters as kept, a finding's
// name, the tier an answer was made by, and an entry as a page reads it. Pure, so the list, the page and the memory
// read one answer, and every case is an assertion.

/**
 * The similarity's slider: its floor, its ceiling, where it starts and its step. Below the floor two documents only
 * share words, so nothing lower is offered (UX5 U42). The useful value depends on the embedder and the corpus:
 * measured on this family, 0.82 returns nothing, 0.70 the true pairs, 0.60 begins pulling in unrelated documents.
 */
export const SIMILARITY = { min: 0.5, max: 0.95, initial: 0.75, step: 0.01 } as const;

/** In the slider's own steps, so a kept value and a dragged one are the same number. */
const stepped = (value: number) => Math.round(value * 100) / 100;

/** One step down from an empty answer: a tenth, never below the floor, in the slider's own steps. */
export const lower = (value: number) => Math.max(SIMILARITY.min, stepped(value - 0.1));

/** At the floor, where an empty answer offers nothing lower and says so. */
export const atFloor = (value: number) => value <= SIMILARITY.min + 0.001;

/** Search's filters as kept (`daoris.list.search.filters`). */
export type SearchFilters = {
  /**
   * Each repository's own entries only, the default: canonical content is byte-identical in every adopter, so
   * including it returns a dozen copies of one rule and calls that a corpus.
   */
  localOnly: boolean;
};

/** What Search's list keeps, read back: anything but an explicit `false` is the default. */
export function searchFilters(kept: Record<string, unknown>): SearchFilters {
  return { localOnly: kept.localOnly !== false };
}

/** Convergence's filters as kept (`daoris.list.convergence.filters`): the similarity its findings are asked at. */
export type ConvergenceFilters = { threshold: number };

/** What Convergence's list keeps, read back within the slider's ends and in its steps; anything else is its start. */
export function convergenceFilters(kept: Record<string, unknown>): ConvergenceFilters {
  const value = kept.threshold;
  if (typeof value !== 'number' || !Number.isFinite(value)) return { threshold: SIMILARITY.initial };
  return { threshold: Math.min(SIMILARITY.max, Math.max(SIMILARITY.min, stepped(value))) };
}

/**
 * A finding's name, as its list keeps it chosen: the entries it holds, in one order. The service gives a finding no id
 * of its own, and its entries are what it is: the same entries found again at another similarity are the same finding,
 * and an entry's id is its place, so editing a file does not make it another (`KnowledgeEntry.Id`).
 */
export function findingId(finding: Pick<Convergence, 'entries'>): string {
  return finding.entries.map((entry) => entry.id).sort().join('\n');
}

/** A finding's title: its entries' titles, each once, in the service's order. Two copies of one rule share a name. */
export function findingTitle(finding: Pick<Convergence, 'entries'>): string {
  return [...new Set(finding.entries.map((entry) => entry.title))].join(' · ');
}

/**
 * Which tier made a search's answer (TIER1, D24): the one that ANSWERED, from the service's header, never the one
 * configured; a host older than the header answers none, and the configured tier is then all there is to say. `none`
 * is no half answering, which is not the same as nothing matching.
 */
export function answeredBy(tier: string | null, semantic: boolean): { byMeaning: boolean; nothing: boolean } {
  return { byMeaning: tier === null ? semantic : tier.includes('semantic'), nothing: tier === 'none' };
}

/**
 * An entry as a page reads it: its text; on its way; gone, where the service holds nothing by its id now (moved,
 * renamed, removed, or its repository retired); or the sentence of a read that failed, which is not the same.
 */
export type EntryReading =
  | { state: 'read'; entry: Entry }
  | { state: 'loading' }
  | { state: 'gone' }
  | { state: 'unanswered'; sentence: string };

/** One entry's read, as the query layer answers it, made a reading. */
export function readingOf(read: { data?: Entry; error: unknown }): EntryReading {
  if (read.data) return { state: 'read', entry: read.data };
  if (read.error) return notFound(read.error) ? { state: 'gone' } : { state: 'unanswered', sentence: sentence(read.error) };
  return { state: 'loading' };
}
