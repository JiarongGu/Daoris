import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getBridge, useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from '../queries';
import { type ChatMessage, type EventPage, mergeEvents, type SessionEvent } from '../work/conversation';
import type { SessionOption } from '../work/SessionOptions';
import type { Reach } from '../work/say';
import { toUpload } from '../attachments';
import { call } from './call';

// A session's conversation (MOD3): its record read back and followed live, its turns, its options,
// and the verbs that start, speak in and finish one (D76).

/**
 * A session's conversation (D76, CONV1): its record read back a page at a time, and its live events
 * merged in as the driver writes them.
 *
 * @remarks
 * **The console's two-source rule, for events.** The newest page is asked for once on open, and
 * live batches arrive as `SESSION_EVENTS`. The driver's sequence numbers make them one record:
 * anything already held is dropped, and a batch that skips ahead is a batch that missed something,
 * closed by asking for what came `after` the last held — never shown as two halves joined.
 *
 * **It outlives a restart**, which the console does not: the history is the record under the home,
 * so a conversation that happened while this window was closed still reads back.
 *
 * Desktop-only for the console's reason (D47 §4): a browser has no bridge, and holds nothing here.
 */
export function useSessionEvents(sessionId: string | null) {
  const { isAvailable } = useShenora();
  const [events, setEvents] = useState<SessionEvent[]>([]);
  const [earlier, setEarlier] = useState(false);
  // What the session was first asked, where the newest page does not hold it (SESS1): a long run reads from it.
  const [opening, setOpening] = useState<SessionEvent | null>(null);
  // Where the first failed call began, wherever it is in the run (SESS1 S9) — the jump's target.
  const [firstFailure, setFirstFailure] = useState<number | null>(null);
  // Whether the history has answered, so an empty record reads as "nothing said" only once it is one.
  const [loaded, setLoaded] = useState(false);
  // The newest sequence held, and which session it belongs to — read inside the event handler, which
  // must not re-subscribe every time an event arrives.
  const latest = useRef(0);
  const attended = useRef<string | null>(null);

  const history = useCallback(
    (payload: Record<string, unknown>) =>
      getBridge().invoke<EventPage>('DAORIS.DRIVER', 'SESSION_HISTORY', { payload }),
    [],
  );

  const hold = useCallback((incoming: readonly SessionEvent[]) => {
    if (incoming.length === 0) return;
    latest.current = Math.max(latest.current, incoming[incoming.length - 1]!.seq);
    setEvents((held) => mergeEvents(held, incoming));
  }, []);

  useEffect(() => {
    attended.current = sessionId;
    latest.current = 0;
    setEvents([]);
    setEarlier(false);
    setOpening(null);
    setFirstFailure(null);
    setLoaded(false);
    if (!isAvailable || !sessionId) return;

    let current = true;
    void history({ id: sessionId })
      .then((page) => {
        if (!current || !page) return;
        hold(page.events ?? []);
        setEarlier(Boolean(page.earlier));
        setOpening(page.opening ?? null);
        setFirstFailure(typeof page.firstFailure === 'number' ? page.firstFailure : null);
      })
      // A record that failed to load is a quiet absence: the session's head above it is already there.
      .catch(() => {})
      .finally(() => { if (current) setLoaded(true); });

    return () => { current = false; };
  }, [isAvailable, sessionId, history, hold]);

  useShenoraEvent('DAORIS', 'SESSION_EVENTS', (payload) => {
    const batch = payload as { session?: string; events?: SessionEvent[] } | undefined;
    const id = attended.current;
    if (!id || batch?.session !== id || !batch.events?.length) return;

    const first = batch.events[0]!.seq;
    if (latest.current > 0 && first > latest.current + 1) {
      void history({ id, after: latest.current })
        .then((page) => { if (attended.current === id) hold(page?.events ?? []); })
        .catch(() => {});
      return;
    }

    hold(batch.events);
  });

  /** The page before the oldest held — "load earlier". */
  const loadEarlier = useCallback(async () => {
    const id = attended.current;
    const oldest = events[0]?.seq;
    if (!id || oldest === undefined) return;
    try {
      const page = await history({ id, before: oldest });
      if (attended.current !== id || !page) return;
      setEvents((held) => mergeEvents(held, page.events ?? []));
      setEarlier(Boolean(page.earlier));
    } catch {
      // Nothing more to show is the same to a reader as nothing more held; the button stays.
    }
  }, [events, history]);

  return { events, opening, firstFailure, earlier, loaded, loadEarlier };
}

/** Where a conversation's turns stand, as the driver holds them (CONV4a). */
export type SessionTurns = {
  /** What the person sent that has not reached the harness yet, in the order sent, with its files' names. */
  queued: ChatMessage[];
  /** A turn is on its way to the harness or running there — what stopping the turn acts on. */
  taking: boolean;
  /**
   * The conversation's door is still opening (HELP4): what is queued waits for it, not for a turn to
   * end. Absent where the driver did not say.
   */
  opening?: boolean;
  /**
   * A driven session hears what the person adds (SESS3): its words are held and become its next prompt.
   * Absent for a conversation, which always does, and false for a driven session nothing could hear.
   */
  listening?: boolean;
  /**
   * When its last turn ended on this machine (RAIL2): a live chat's last move, which its record — moved
   * on state changes only — never says. Absent before any turn has ended here.
   */
  lastTurn?: string;
};

/** Nothing known: nothing waiting and nothing to stop — what the composer offers until the driver answers. */
export const NO_TURNS: SessionTurns = { queued: [], taking: false };

/** Messages as the driver told them, read defensively: anything that is not one is left out. */
const messagesOf = (list: unknown): ChatMessage[] =>
  (Array.isArray(list) ? list : []).flatMap((item) => {
    const message = item as Partial<ChatMessage> | null;
    if (typeof message?.text !== 'string') return [];
    return [{
      text: message.text,
      files: Array.isArray(message.files) ? message.files.filter((name): name is string => typeof name === 'string') : [],
    }];
  });

/** The driver's answer, read defensively: anything that is not a queue is nothing waiting and nothing running. */
const turnsOf = (answer: unknown): SessionTurns => {
  const held = answer as {
    queued?: unknown; taking?: unknown; opening?: unknown; listening?: unknown; lastTurn?: unknown;
  } | null | undefined;
  return {
    queued: messagesOf(held?.queued), taking: held?.taking === true,
    ...(held?.opening === true ? { opening: true } : {}),
    ...(typeof held?.listening === 'boolean' ? { listening: held.listening } : {}),
    ...(typeof held?.lastTurn === 'string' && held.lastTurn ? { lastTurn: held.lastTurn } : {}),
  };
};

/** Whether the driver's answer says anything about a turn: one that does not is no answer at all. */
const answersTurn = (answer: unknown): boolean =>
  typeof (answer as { taking?: unknown } | null | undefined)?.taking === 'boolean';

/**
 * Where each live conversation's turns stand (CONV4b, UX5 U17): whether one is in flight, and what
 * is waiting behind it — asked for once per conversation, then followed as `SESSION_QUEUED`.
 *
 * @remarks
 * **The driver is the authority**, not the record: the record learns a turn began when its first
 * event lands, a waiting message is in no record at all, and a chat's record says `working` for as
 * long as its process lives, between turns too. So the composer's stop and its queue follow this,
 * and so do the rail and the head, which read a chat between turns as idle (`shownState`).
 *
 * **One listener for every conversation**, where the composer's own hook listened for the attended
 * one: the rail needs them all, and two listeners for one event are two subscriptions to keep in
 * step. A conversation the driver has not answered for is absent from the answer, never "not
 * taking": nothing known is not a claim that nothing runs.
 *
 * Each live answer is the whole state, so a missed one costs nothing. Desktop-only: a conversation
 * is a process on this machine.
 */
export function useChatTurns(ids: readonly string[]): Record<string, SessionTurns> {
  const { isAvailable } = useShenora();
  const [turns, setTurns] = useState<Record<string, SessionTurns>>({});
  const key = [...new Set(ids)].sort().join('\n');
  const watched = useRef<ReadonlySet<string>>(new Set());

  useEffect(() => {
    const wanted = key ? key.split('\n') : [];
    watched.current = new Set(wanted);
    // What is no longer watched is forgotten, so a conversation opened again is asked again.
    setTurns((held) => Object.fromEntries(Object.entries(held).filter(([id]) => watched.current.has(id))));
    if (!isAvailable) return;

    let current = true;
    for (const id of wanted) {
      void getBridge().invoke<unknown>('DAORIS.DRIVER', 'SESSION_QUEUE', { payload: { id } })
        .then((answer) => {
          if (current && watched.current.has(id) && answersTurn(answer)) {
            setTurns((held) => ({ ...held, [id]: turnsOf(answer) }));
          }
        })
        // Nothing known is nothing claimed: the composer offers what it can prove.
        .catch(() => {});
    }
    return () => { current = false; };
  }, [isAvailable, key]);

  useShenoraEvent('DAORIS', 'SESSION_QUEUED', (payload) => {
    const state = payload as { session?: string } | undefined;
    const session = state?.session;
    if (!session || !watched.current.has(session) || !answersTurn(state)) return;
    setTurns((held) => ({ ...held, [session]: turnsOf(state) }));
  });

  return turns;
}

/** A conversation's options as the driver told them, read defensively: anything that is not one is left out. */
const optionsOf = (answer: unknown): SessionOption[] => {
  const list = (answer as { options?: unknown } | null | undefined)?.options;
  return (Array.isArray(list) ? list : []).flatMap((item) => {
    const option = item as Partial<SessionOption> | null;
    if (typeof option?.id !== 'string' || typeof option.name !== 'string' || typeof option.current !== 'string') return [];
    return [{
      id: option.id,
      name: option.name,
      category: typeof option.category === 'string' ? option.category : null,
      current: option.current,
      choices: (Array.isArray(option.choices) ? option.choices : []).flatMap((choice) =>
        (typeof choice?.value === 'string' && typeof choice.name === 'string'
          ? [{ value: choice.value, name: choice.name, description: choice.description ?? null }]
          : [])),
    }];
  });
};

/**
 * One conversation's model and effort, as its agent offered them on the protocol door (AGT6b, D98): asked
 * of the driver once, then followed as `SESSION_OPTIONS_CHANGED` — after a change, and when the agent
 * changed them itself. None offered, or a conversation the driver does not hold, is an empty list.
 * Desktop-only: it is a live process on this machine.
 */
export function useSessionOptions(session: string | null): SessionOption[] {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const query = useQuery({
    queryKey: keys.sessionOptions(session ?? ''),
    queryFn: async () => optionsOf(await call<unknown>('SESSION_OPTIONS', { id: session })),
    enabled: isAvailable && session !== null,
  });

  useShenoraEvent('DAORIS', 'SESSION_OPTIONS_CHANGED', (payload) => {
    const told = payload as { session?: unknown } | undefined;
    if (typeof told?.session !== 'string') return;
    client.setQueryData(keys.sessionOptions(told.session), optionsOf(told));
  });

  return session !== null ? query.data ?? [] : [];
}

/**
 * Change one of a conversation's options (AGT6b, D98): `session/set_config_option` on its session, through
 * the driver, which refuses the mode and anything the agent never offered. The answer is the options after
 * the change, which is what the composer shows from then on.
 */
export const useSetSessionOption = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (change: { id: string; option: string; value: string }) =>
      optionsOf(await call<unknown>('SET_SESSION_OPTION', change)),
    onSuccess: (options, change) => client.setQueryData(keys.sessionOptions(change.id), options),
  });
};

/** What stopping a turn did (CONV4a): whether a turn was asked to stop, and what came back unsent. */
export type TurnStop = { cancelled: boolean; withdrawn: ChatMessage[] };

/**
 * Stop the conversation's turn and keep the conversation (CONV4a) — the third verb, beside finishing
 * it and stopping it. What was waiting comes back, for the composer to hand to the person.
 */
export const useCancelTurn = () =>
  useMutation({
    mutationFn: async (id: string): Promise<TurnStop> => {
      const answer = await call<{ cancelled?: unknown; withdrawn?: unknown }>('CANCEL_TURN', { id });
      return { cancelled: answer?.cancelled === true, withdrawn: messagesOf(answer?.withdrawn) };
    },
  });

/**
 * A conversation with an agent in one repository (D49 §3).
 *
 * @remarks
 * Shell-only, because a chat is a process on this machine and processes never leave the driver
 * (D46 §7). The record is the service's — a teammate sees that a chat happened — and only the stream
 * and the typing are here.
 *
 * Daoris makes no model calls: the harness carries the model and the conversation, and these three
 * verbs move text and nothing else (D24, `model-decoupling`).
 */
export const useStartChat = () => {
  const client = useQueryClient();
  return useMutation({
    // Every part after the repository is omittable, and omitted means something: the profile takes
    // the workspace's default, then the machine's, then the harness's own configuration home (D49
    // §4); the adapter takes `driver.json`'s; and the tree falls back to the repository's standing
    // opt-in (D51). The same resolution a driven session gets, so a conversation is not a second
    // set of rules — which is why each is dropped from the payload rather than sent as a null.
    mutationFn: (start: {
      repository: string; profile?: string; adapter?: string; ownTree?: boolean;
    }) => call<{ sessionId: string | null; message: string }>('START_CHAT', {
      repository: start.repository,
      ...(start.profile ? { profile: start.profile } : {}),
      ...(start.adapter ? { adapter: start.adapter } : {}),
      ...(start.ownTree ? { ownTree: true } : {}),
    }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.driver });
    },
  });
};

/**
 * What the person's words to a session did (MSG1d, D137 §5.3), as `SESSION_INPUT` answers for every state: `sent` when
 * they are held or taken, when they reach the session (`next-step`, `turn-end` or `resume`), and why nothing takes them,
 * by a code, where nothing does.
 */
export type WordsAnswer = { sent: boolean; reaches: string | null; why: string | null };

/**
 * An answer read defensively. The bridge leaves a null out, so an absent `reaches` or `why` is null, and an answer that is
 * not one sent nothing.
 */
const wordsOf = (answer: unknown): WordsAnswer => {
  const said = answer as { sent?: unknown; reaches?: unknown; why?: unknown } | null | undefined;
  return {
    sent: said?.sent === true,
    reaches: typeof said?.reaches === 'string' && said.reaches ? said.reaches : null,
    why: typeof said?.why === 'string' && said.why ? said.why : null,
  };
};

/** What a word said now would do, read from `SESSION_QUEUE`'s answer (MSG1d): its `reaches` and `why`, each null where absent. */
const reachOf = (answer: unknown): Reach => {
  const { reaches, why } = wordsOf(answer);
  return { reaches, why };
};

/**
 * What a word said now to this session would do (MSG1d, D137 §5.3): `SESSION_QUEUE`'s `reaches` and `why`, which decide
 * the box the page offers and the line it draws where nothing takes words (`boxOf`). Undefined until the driver answers.
 *
 * @remarks
 * **Asked again whenever what it reads moves**: keyed by the session's state and whether its inbox listens, so a session
 * that winds up, ends, or goes on is asked again as it moves, and under the sessions' key, so whatever asks the listing
 * again (a word kept, a tick) asks this too. The live queue (`SESSION_QUEUED`) does not carry it. Desktop-only, as a
 * conversation is.
 */
export const useSessionReach = (session: { id: string; state: string } | null, listening = false): Reach | undefined => {
  const { isAvailable } = useShenora();
  const answer = useQuery({
    queryKey: keys.sessionReach(session?.id ?? '', session?.state ?? '', listening),
    queryFn: async () => reachOf(await call<unknown>('SESSION_QUEUE', { id: session!.id })),
    enabled: isAvailable && session !== null,
    staleTime: 30_000,
    refetchOnWindowFocus: false,
    retry: false,
  });
  return session ? answer.data : undefined;
};

/**
 * One message into the session (CONV4), and what it did (MSG1d): held or taken, when it reaches the session, or why
 * nothing takes it.
 *
 * @remarks
 * Its files (CONV4c) go as names and bytes, the way a quest's uploads do, and the driver keeps them
 * for the conversation outside its tree — the page never learns where. A message with none sends none.
 */
export const useSendMessage = () =>
  useMutation({
    // A preface (HELP1b) is where the person is, handed to the agent ahead of the words; absent for most.
    mutationFn: async (message: { id: string; text: string; files?: File[]; preface?: string }) => {
      const files = message.files?.length ? await Promise.all(message.files.map(toUpload)) : [];
      return wordsOf(await call<unknown>('SESSION_INPUT', {
        id: message.id, text: message.text, ...(files.length > 0 ? { files } : {}),
        ...(message.preface ? { preface: message.preface } : {}),
      }));
    },
  });

/**
 * The person's words to a session that is not a live chat (MSG1f, D137 §5.1): a working driven session's running door, a
 * park's answer, a session that ended, or one winding up, all through `SESSION_INPUT`, which keeps a park's and an ended
 * record's words on the record and nudges the driver's loop (MSG1d). Words alone: the resumed run is handed no files.
 *
 * The sessions are asked again, so a park shows its answer and a record its words waiting.
 */
export const useSay = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (words: { id: string; text: string }) =>
      wordsOf(await call<unknown>('SESSION_INPUT', { id: words.id, text: words.text })),
    onSuccess: () => void client.invalidateQueries({ queryKey: keys.allSessions }),
  });
};

/** What a go-ahead answered on a park's page did (KNOWUSE1a2): the go-ahead's sentence, then what became of the park. */
export type GoAheadAnswered = WordsAnswer & { message: string };

/**
 * A go-ahead a parked session asked, answered on the session's own page (KNOWUSE1a2, D135 §2): one press, where the ask's
 * page and the box took two. `SESSION_GO_AHEAD` answers the go-ahead on its ask first, so the conversation the answer
 * resumes is handed it, then the park: with the person's words as the box keeps them, or, with none, the park's blank
 * answer. A refused go-ahead answers nothing else, in the service's sentence. The person's words go only where they gave
 * some: the record keeps no words they did not write (D137).
 *
 * The sessions and the asks are asked again whatever came of it, since a park that could not be answered may still have
 * had its go-ahead answered.
 */
export const useParkGoAhead = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (answer: { id: string; ask: string; number: number; approved: boolean; words?: string }): Promise<GoAheadAnswered> => {
      const answered = await call<unknown>('SESSION_GO_AHEAD', {
        id: answer.id, ask: answer.ask, number: answer.number, approved: answer.approved,
        ...(answer.words ? { words: answer.words } : {}),
      });
      const message = (answered as { message?: unknown } | null | undefined)?.message;
      return { ...wordsOf(answered), message: typeof message === 'string' ? message : '' };
    },
    onSettled: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.allAsks });
    },
  });
};

/**
 * What *Start a conversation with these words* did (MSG1f2): the conversation it opened, whether the words are its first
 * message and off the session they were written to, why nothing was done by a code where it refused, and the driver's
 * sentence.
 */
export type StartedFrom = { sessionId: string | null; sent: boolean; why: string | null; message: string };

/** The press's answer read defensively: the bridge leaves a null out, and an answer that is not one started nothing. */
const startedOf = (answer: unknown): StartedFrom => {
  const started = answer as { sessionId?: unknown; sent?: unknown; why?: unknown; message?: unknown } | null | undefined;
  return {
    sessionId: typeof started?.sessionId === 'string' && started.sessionId ? started.sessionId : null,
    sent: started?.sent === true,
    why: typeof started?.why === 'string' && started.why ? started.why : null,
    message: typeof started?.message === 'string' ? started.message : '',
  };
};

/**
 * *Start a conversation with these words* (MSG1f2, D137 §2.2, §5.3): `SESSION_START_FROM`, one act of the driver's. A new
 * chat in the session's repository whose first message is the words the session could not go on with, handed with a
 * preface naming it, which is the agent's to read; then the words leave that session's record naming the new one, and its
 * conversation says where they went. What it refuses, it refuses before anything starts, by a code.
 */
export const useStartFrom = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: async (from: { session: string }): Promise<StartedFrom> =>
      startedOf(await call<unknown>('SESSION_START_FROM', { id: from.session })),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.allSessions });
      void client.invalidateQueries({ queryKey: keys.driver });
    },
  });
};

/**
 * Finish a conversation: the harness gets end-of-input, says what it was going to say, and exits.
 * `useStopSession` is the other verb and means something else — the person cut it off.
 */
export const useEndChat = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => call<{ ended: boolean }>('END_CHAT', { id }),
    onSuccess: () => void client.invalidateQueries({ queryKey: keys.allSessions }),
  });
};
