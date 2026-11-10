import { useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { type Place, readPlace } from '../help/focus';
import { call } from './call';

/** What one Ask Daoris turn did beyond its conversation's focus, as Daoris's doors saw it (the driver's `HelpBeyondEntry`). */
export type BeyondEntry = {
  at: string;
  act: 'search' | 'read' | 'propose' | 'press';
  /** The place; this machine for every workspace. */
  place: Place;
  /** A search's words, a read's path, a proposal's kind; a press's `MODULE.ROUTE`, or the connector's tool. */
  what: string | null;
  /** A full-access press's outcome; none for what the connector noted, which it noted once answered. */
  outcome: 'running' | 'done' | 'failed' | null;
};

const ACTS: readonly BeyondEntry['act'][] = ['search', 'read', 'propose', 'press'];
const OUTCOMES: readonly NonNullable<BeyondEntry['outcome']>[] = ['running', 'done', 'failed'];
const text = (value: unknown): string | null => (typeof value === 'string' && value.trim() ? value : null);

/** One `HELP_BEYOND` answer read entry by entry: one with no time, an act not known or no place is left out. */
export function beyondAnswer(raw: unknown): BeyondEntry[] {
  const entries = raw && typeof raw === 'object' ? (raw as Record<string, unknown>).entries : null;
  if (!Array.isArray(entries)) return [];
  return entries.flatMap((each): BeyondEntry[] => {
    if (!each || typeof each !== 'object') return [];
    const shaped = each as Record<string, unknown>;
    const at = text(shaped.at);
    const act = ACTS.find((known) => known === shaped.act);
    const place = readPlace(shaped.place);
    if (at === null || !act || place === null) return [];
    return [{ at, act, place, what: text(shaped.what), outcome: OUTCOMES.find((known) => known === shaped.outcome) ?? null }];
  });
}

/** The receipts' key: a conversation's under it. Asked again as its sessions move and as a press is told. */
export const HELP_BEYOND = ['help-beyond'] as const;

/**
 * What an Ask Daoris conversation looked at and did beyond its focus, first to last (`HELP_BEYOND`): the page draws one receipt under
 * each turn from it. A read of this machine's home, desktop-only like the conversation.
 */
export const useHelpBeyond = (session: string | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: [...HELP_BEYOND, session],
    queryFn: async () => beyondAnswer(await call('HELP_BEYOND', { session: session! })),
    enabled: isAvailable && session !== null,
  });
};
