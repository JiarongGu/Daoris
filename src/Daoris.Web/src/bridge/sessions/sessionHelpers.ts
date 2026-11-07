import { useMutation, useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { readPieces } from '../../work/pieces/pieceRows';
import type { HandOverAskRow } from '../../work/waits/HandOverAgentAsk';
import type { HelperAskRow, HelperWait } from '../../work/waits/HelperAsks';
import { type ParkQuestion, readQuestion } from '../../work/waits/parkQuestion';
import { call } from '../call';

/** What *Ask another agent…* answers: the ask's id where it is written, else why not by its code, and the driver's sentence. */
export type HelperAsked = { asked: string | null; why: string | null; message: string };

/** What the person's yes to an agent's ask answered: whether the ask has it, else why not by its code. */
export type HelperApproved = { approved: boolean; why: string | null; message: string };

/** What the person's no to an agent's ask answered: whether the ask has it, else why not by its code. */
export type HelperDeclined = { declined: boolean; why: string | null; message: string };

export const word = (value: unknown): string | null => (typeof value === 'string' && value ? value : null);

/**
 * *Ask another agent…* : the person's own ask of a helper for a session, the
 * file `daoris session helper` writes, for the driver's next look to take. The question as written and the agent the person
 * named, none where Daoris chooses. Read field by field, so a refusal keeps its code for the page to word. The sessions and the
 * driver are asked again by the state hub, and with the sessions the session's asks, which sit under their key.
 */
export const useAskHelper = () => useMutation({
  mutationFn: async (ask: { session: string; question: string; agent?: string | null; after?: string; toPerson?: boolean }): Promise<HelperAsked> => {
    // *Ask again…*: the ask it follows, and from a member's thread an answer kept for the person.
    const answer = (await call('SESSION_HELPER_ASK', {
      id: ask.session, question: ask.question, ...(ask.agent ? { agent: ask.agent } : {}),
      ...(ask.after ? { after: ask.after } : {}), ...(ask.toPerson ? { toPerson: true } : {}),
    })) as { asked?: unknown; why?: unknown; message?: unknown } | null;
    return { asked: word(answer?.asked), why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/** What *Pass to the lead* answered: whether the answer is passed, else why not by its code (`gone`, `lead`, `unanswered`). */
export type HelperPassed = { passed: boolean; why: string | null; message: string };

/**
 * *Pass to the lead*: an answer the person asked for themselves sent to the session that asked, the
 * helper's word with its mark unchanged and the person's press beside it, at the driver's next look. Read field by field.
 */
export const usePassHelper = () => useMutation({
  mutationFn: async (pass: { session: string; ask: string }): Promise<HelperPassed> => {
    const answer = (await call('SESSION_HELPER_PASS', { session: pass.session, ask: pass.ask })) as
      { passed?: unknown; why?: unknown; message?: unknown } | null;
    return { passed: answer?.passed === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/** The asks a session made of helpers, under the sessions' key, so whatever asks the records again asks these too. */
const helpersKey = (session: string) => ['sessions', 'helpers', session] as const;

/** The rows an answer holds, read defensively: one with no id or no question is left out rather than guessed at. */
const readHelpers = (answer: unknown): HelperAskRow[] => {
  const rows = (answer as { helpers?: unknown } | null)?.helpers;
  if (!Array.isArray(rows)) return [];
  return rows.filter((row): row is Record<string, unknown> => Boolean(row) && typeof row === 'object')
    .filter((row) => word(row.id) !== null && typeof row.question === 'string')
    .map((row) => ({
      id: row.id as string,
      question: row.question as string,
      agent: word(row.agent),
      askedBy: word(row.askedBy) ?? 'agent',
      at: typeof row.at === 'string' ? row.at : '',
      state: word(row.state),
      code: word(row.code),
      helper: word(row.helper),
      startedOn: word(row.startedOn),
      // A cooling ask's account's reset.
      until: word(row.until),
      // The ask it follows, where its answer goes, and whether the person's answer waits on their pass or is passed.
      after: word(row.after),
      to: word(row.to),
      kept: row.kept === true,
      passed: row.passed === true,
    }));
};

/**
 * The asks a session made of helpers, oldest first, each as the driver's look left it, read off this machine's own
 * files: shell-only, and asked only for a session of this machine's. Its page lists them, with the person's yes where an agent's
 * ask waits on it. A look moves them on its own; its record's change, a helper started or an answer handed, asks them again.
 */
export const useSessionHelpers = (session: string | null | undefined) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: helpersKey(session ?? ''),
    queryFn: async () => readHelpers(await call('SESSION_HELPERS', { id: session ?? '' })),
    enabled: isAvailable && Boolean(session),
    refetchOnWindowFocus: false,
  });
};

/** What *Ask another agent to work on a piece…* answers: the piece's id where it is written, else why not by its code. */
export type PartnerAsked = { asked: string | null; why: string | null; message: string };

/**
 * *Ask another agent to work on a piece…*: the person's own ask of a
 * piece for a session, the file `daoris session partner` writes at the commit the session's tree stands at, for the driver's
 * next look to judge. The piece as written, the agent the person named and the paths it is about. Read field by field, so a refusal
 * keeps its code for the page to word. The sessions and the driver are asked again by the state hub, and with the sessions the
 * session's pieces, which sit under their key.
 */
export const useAskPartner = () => useMutation({
  mutationFn: async (ask: { session: string; piece: string; agent: string; paths?: readonly string[] }): Promise<PartnerAsked> => {
    const answer = (await call('SESSION_PARTNER_ASK', {
      id: ask.session, piece: ask.piece, agent: ask.agent, ...(ask.paths && ask.paths.length > 0 ? { paths: [...ask.paths] } : {}),
    })) as { asked?: unknown; why?: unknown; message?: unknown } | null;
    return { asked: word(answer?.asked), why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/** What a piece's act (*Discard…*, *Hand over what it left*) answered: whether the driver's file holds it, else why not by its code. */
export type PartnerActed = { acted: boolean; why: string | null; message: string };

/**
 * A piece's later act: the person's *Discard…* or *Hand over what it left* on one of a
 * session's pieces, the file the driver's next look takes and does. A refusal keeps its code for the page to word. The sessions and the
 * driver are asked again by the state hub, and with them the session's pieces.
 */
export const usePartnerAct = () => useMutation({
  mutationFn: async (act: { session: string; piece: string; act: 'discard' | 'hand-over' }): Promise<PartnerActed> => {
    const answer = (await call('SESSION_PARTNER_ACT', { id: act.session, piece: act.piece, act: act.act })) as
      { acted?: unknown; why?: unknown; message?: unknown } | null;
    return { acted: answer?.acted === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/**
 * The pieces a session asked, oldest first, each as the driver's look left it, read off this
 * machine's own files: shell-only, and asked only for a session of this machine's. Under the sessions' key, so a look's tick and the
 * session's moves ask them again, as its helpers' asks are.
 */
export const useSessionPartners = (session: string | null | undefined) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: ['sessions', 'partners', session ?? ''] as const,
    queryFn: async () => readPieces(await call('SESSION_PARTNERS', { id: session ?? '' })),
    enabled: isAvailable && Boolean(session),
    refetchOnWindowFocus: false,
  });
};

/**
 * The person's yes to an agent's ask under the `helpers` rule's `ask`: the `approved` state
 * the driver's look reads, by the session and the ask. A refusal is the answer, by its code. The sessions and the driver are asked
 * again by the state hub.
 */
export const useApproveHelper = () => useMutation({
  mutationFn: async (yes: { session: string; ask: string }): Promise<HelperApproved> => {
    const answer = (await call('SESSION_HELPER_APPROVE', {
      session: yes.session, ask: yes.ask,
    })) as { approved?: unknown; why?: unknown; message?: unknown } | null;
    return { approved: answer?.approved === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/**
 * The person's no to an agent's ask the driver's look holds waiting on their word: the ask refused
 * `declined` and its note on the asking session's record, at once, by the session and the ask. A refusal is the answer, by its
 * code. The sessions and the driver are asked again by the state hub.
 */
export const useDeclineHelper = () => useMutation({
  mutationFn: async (no: { session: string; ask: string }): Promise<HelperDeclined> => {
    const answer = (await call('SESSION_HELPER_DECLINE', {
      session: no.session, ask: no.ask,
    })) as { declined?: unknown; why?: unknown; message?: unknown } | null;
    return { declined: answer?.declined === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '' };
  },
});

/**
 * A session's agent's own ask to hand it over, as the driver's look leaves it, read off this machine's own
 * files: shell-only, and asked only for a session of this machine's; null where its agent asked none. Under the sessions' key, so a
 * look's tick and the session's moves ask it again, as its helpers' asks are.
 */
export const useSessionHandOverAsk = (session: string | null | undefined) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: ['sessions', 'handOverAsk', session ?? ''] as const,
    queryFn: async (): Promise<HandOverAskRow | null> => {
      const ask = ((await call('SESSION_HAND_OVER_ASK', { id: session ?? '' })) as { ask?: unknown } | null)?.ask as Record<string, unknown> | null;
      if (!ask || typeof ask !== 'object' || word(ask.id) === null || typeof ask.note !== 'string') return null;
      return {
        id: ask.id as string, note: ask.note, at: typeof ask.at === 'string' ? ask.at : '',
        state: word(ask.state), code: word(ask.code), agent: word(ask.agent),
      };
    },
    enabled: isAvailable && Boolean(session),
    refetchOnWindowFocus: false,
  });
};

/**
 * A parked session's question to the person, as its agent asked it through `question_ask` and
 * the driver kept it for the park its last turn ended in, read off this machine's own files: shell-only, and asked only for a park of
 * this machine's still waiting on the person (null asks nothing); null where none is kept. Under the sessions' key, so a look's tick
 * and the session's moves ask it again, as its hand-over ask is.
 */
export const useSessionQuestion = (session: string | null | undefined) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: ['sessions', 'question', session ?? ''] as const,
    queryFn: async (): Promise<ParkQuestion | null> => readQuestion(await call('SESSION_QUESTION', { id: session ?? '' })),
    enabled: isAvailable && Boolean(session),
    refetchOnWindowFocus: false,
  });
};

/** What the person's yes or no to their session's agent's ask to hand over came to: whether it stands, its code, the sentence. */
export type HandOverWord = { stands: boolean; why: string | null; message: string; agent: string | null };

/**
 * The person's yes to their session's agent's ask to hand it over: a conversation this shell holds is handed over at
 * once, anything else is `approved` for the driver's look. A refusal is the answer, by its code. The sessions and the driver are
 * asked again by the state hub.
 */
export const useApproveHandOver = () => useMutation({
  mutationFn: async (yes: { session: string; ask: string }): Promise<HandOverWord> => {
    const answer = (await call('SESSION_HAND_OVER_APPROVE', {
      session: yes.session, ask: yes.ask,
    })) as { approved?: unknown; why?: unknown; message?: unknown; agent?: unknown } | null;
    return {
      stands: answer?.approved === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '',
      agent: word(answer?.agent),
    };
  },
});

/** The person's no to their session's agent's ask to hand it over: refused `declined`, its note on the record, at once. */
export const useDeclineHandOver = () => useMutation({
  mutationFn: async (no: { session: string; ask: string }): Promise<HandOverWord> => {
    const answer = (await call('SESSION_HAND_OVER_DECLINE', {
      session: no.session, ask: no.ask,
    })) as { declined?: unknown; why?: unknown; message?: unknown } | null;
    return { stands: answer?.declined === true, why: word(answer?.why), message: typeof answer?.message === 'string' ? answer.message : '', agent: null };
  },
});

/**
 * What of the agents' asks waits on the person's word across this machine's sessions: *What needs you*'s rows,
 * oldest first, read off this machine's own files, so shell-only. Under the sessions' key, so every tick asks it again, and the
 * driver's look says a new one in its report, which is a tick. A row with no id, session or question is left out.
 */
export const useHelperWaits = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: ['sessions', 'helperWaits'] as const,
    queryFn: async (): Promise<HelperWait[]> => {
      const rows = (await call<{ waits?: unknown } | null>('HELPER_WAITS'))?.waits;
      if (!Array.isArray(rows)) return [];
      return rows.filter((row): row is Record<string, unknown> => Boolean(row) && typeof row === 'object')
        .filter((row) => word(row.id) !== null && word(row.session) !== null && typeof row.question === 'string')
        .map((row) => ({
          id: row.id as string,
          session: row.session as string,
          question: row.question as string,
          agent: word(row.agent),
          at: typeof row.at === 'string' ? row.at : '',
        }));
    },
    enabled: isAvailable,
  });
};
