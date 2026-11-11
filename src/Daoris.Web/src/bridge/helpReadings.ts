import { useInfiniteQuery, useMutation, useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { call } from './call';

/** How a reading is shown, from its report alone: a read one with any limit of its evidence is partial, never clean. */
export const READING_STATES = ['queued', 'running', 'read', 'partial', 'left', 'unread', 'disabled'] as const;
export type ReadingState = (typeof READING_STATES)[number];

/** One finding of a reading, a claim and never a fact, with the person's *Seen* and its delivery to the conversation. */
export type ReadingFinding = {
  index: number;
  severity: 'flag' | 'note';
  certainty: 'sure' | 'likely' | 'unsure';
  says: string;
  call: string;
  instruction: string;
  seen: string | null;
  told: { word: string | null; at: string } | null;
};

/** Who read the range: its agent, the model asked and the model its harness reported, and the tokens it reported. */
export type ReadingReader = { agent: string; model: string | null; ran: string | null; tokens: number | null };

/** One range of a conversation's turns as the second check keeps it, drawn as one line under the last turn it covers. */
export type Reading = {
  range: string;
  state: ReadingState;
  opened: string;
  closed: string | null;
  last: string | null;
  turns: string[];
  limits: { code: string; call: string | null }[];
  why: string | null;
  waiting: string | null;
  reader: ReadingReader | null;
  independence: 'another-maker' | 'not-independent' | 'not-counted' | 'unknown' | null;
  findings: ReadingFinding[];
  more: number;
  leave: string | null;
  reasons: { says: string; source: string }[];
};

/** The check as the panel's chip says it: whether it is on, the conversation's ranges pending, and today's spend on the machine. */
export type ReadingsCheck = { on: boolean; pending: number; today: { readings: number; tokens: number; unknown: number } };

export type ReadingsPage = { check: ReadingsCheck; readings: Reading[]; next: string | null; packet?: string | null };

const text = (value: unknown): string | null => (typeof value === 'string' && value.length > 0 ? value : null);
const count = (value: unknown): number => (typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : 0);
const record = (value: unknown): Record<string, unknown> => (value && typeof value === 'object' ? (value as Record<string, unknown>) : {});
const list = (value: unknown): unknown[] => (Array.isArray(value) ? value : []);

function findingOf(raw: unknown): ReadingFinding | null {
  const each = record(raw);
  const severity = each.severity === 'flag' || each.severity === 'note' ? each.severity : null;
  const certainty = each.certainty === 'sure' || each.certainty === 'likely' || each.certainty === 'unsure' ? each.certainty : null;
  if (!severity || !certainty || typeof each.index !== 'number') return null;
  const told = record(each.told);
  return {
    index: each.index, severity, certainty, says: text(each.says) ?? '', call: text(each.call) ?? '', instruction: text(each.instruction) ?? 'none',
    seen: text(each.seen), told: text(told.at) ? { word: text(told.word), at: text(told.at)! } : null,
  };
}

function readingOf(raw: unknown): Reading | null {
  const each = record(raw);
  const state = READING_STATES.find((known) => known === each.state);
  const range = text(each.range);
  if (!state || !range) return null;
  const reader = each.reader ? record(each.reader) : null;
  const independence = (['another-maker', 'not-independent', 'not-counted', 'unknown'] as const).find((known) => known === each.independence) ?? null;
  return {
    range, state, opened: text(each.opened) ?? '', closed: text(each.closed), last: text(each.last),
    turns: list(each.turns).filter((turn): turn is string => typeof turn === 'string'),
    limits: list(each.limits).flatMap((limit) => (text(record(limit).code) ? [{ code: text(record(limit).code)!, call: text(record(limit).call) }] : [])),
    why: text(each.why), waiting: text(each.waiting),
    reader: reader && text(reader.agent)
      ? { agent: text(reader.agent)!, model: text(reader.model), ran: text(reader.ran), tokens: typeof reader.tokens === 'number' ? reader.tokens : null }
      : null,
    independence,
    findings: list(each.findings).map(findingOf).filter((finding): finding is ReadingFinding => finding !== null),
    more: count(each.more), leave: text(each.leave),
    reasons: list(each.reasons).map((reason) => ({ says: text(record(reason).says) ?? '', source: text(record(reason).source) ?? '' })),
  };
}

/** One `HELP_READINGS` answer read: a reading that does not read is left out, and an answer that does not is an empty page. */
export function readingsAnswer(raw: unknown): ReadingsPage {
  const answer = record(raw);
  const check = record(answer.check);
  const today = record(check.today);
  return {
    check: { on: check.on === true, pending: count(check.pending), today: { readings: count(today.readings), tokens: count(today.tokens), unknown: count(today.unknown) } },
    readings: list(answer.readings).map(readingOf).filter((reading): reading is Reading => reading !== null),
    next: text(answer.next),
    ...('packet' in answer ? { packet: text(answer.packet) } : {}),
  };
}

/** A page of readings: the driver's own size, asked by name. */
const READINGS_PAGE = 20;

/** The readings' key: a conversation's pages under it. Asked again as the help scope is told: a report written, a flag seen. */
export const HELP_READINGS = ['help-readings'] as const;

/**
 * A conversation's second-check readings, newest first, 20 a page, each page asked before the last's oldest range
 * (`HELP_READINGS`), with the check's standing on every page. A read of this machine's home, desktop-only like the conversation.
 */
export const useHelpReadings = (session: string | null) => {
  const { isAvailable } = useShenora();
  return useInfiniteQuery({
    queryKey: [...HELP_READINGS, session],
    queryFn: async ({ pageParam }) => readingsAnswer(await call('HELP_READINGS', { session: session!, before: pageParam, limit: READINGS_PAGE })),
    initialPageParam: null as string | null,
    getNextPageParam: (last) => last.next,
    enabled: isAvailable && session !== null,
  });
};

/** One range's packet as kept beside its report, asked only once the person opens it. */
export const useReadingPacket = (session: string, range: string, open: boolean) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: [...HELP_READINGS, session, 'packet', range],
    queryFn: async () => readingsAnswer(await call('HELP_READINGS', { session, range, limit: 1 })).packet ?? null,
    enabled: isAvailable && open,
  });
};

/** The person's *Seen* on one flag (`HELP_READING_SEEN`): acknowledged, never accepted, resolved or answered. */
export const useSeeReading = () =>
  useMutation({
    mutationFn: async (flag: { session: string; range: string; finding: number }): Promise<boolean> =>
      record(await call('HELP_READING_SEEN', flag)).seen === true,
  });
