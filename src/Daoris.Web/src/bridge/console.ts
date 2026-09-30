import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { getBridge, useShenora, useShenoraEvent } from '@shenora/react';
import { call } from './call';

// A session's console and the streams it runs beside itself (MOD3): live lines over the bridge, and
// stopping one task from its tab.

/**
 * A session's console, live (D49 §2).
 *
 * @remarks
 * **Desktop-only, structurally.** Output is transcript-class material — it can carry machine paths,
 * and a transcript never leaves the machine that produced it (D47 §4) — so it arrives over the
 * shell's bridge and has no HTTP route at all. A browser over a keyed remote sees the session's
 * record, as it always did, and never its stream.
 *
 * The backlog is asked for once on open; live lines arrive as `SESSION_OUTPUT` batches. The driver's
 * sequence numbers are what make those two sources one stream: anything already seen is dropped, and
 * a gap — a batch that does not continue where the last one ended — is closed by asking the driver
 * for what is missing rather than by rendering a hole nobody can see.
 */
export type ConsoleLine = { sequence: number; text: string };
export type SessionTail = {
  session: string;
  lines: ConsoleLine[];
  sequence: number;
  /** Whether more is coming. False once the session's process has ended. */
  live: boolean;
  /** Lines that fell out of the driver's bounded window before the page asked. Shown, never hidden. */
  dropped: number;
};

/** The page keeps what the driver keeps: enough to read, never a log file in a render tree. */
const CONSOLE_LINES = 500;

export function useSessionConsole(sessionId: string | null) {
  const { isAvailable } = useShenora();
  const [lines, setLines] = useState<ConsoleLine[]>([]);
  const [live, setLive] = useState(false);
  const [dropped, setDropped] = useState(0);
  // The newest sequence the page holds — read inside the event handler, which must not re-subscribe
  // every time a line arrives.
  const seen = useRef(0);
  // Which session this console is for NOW, so an answer asked for another one is dropped (REV3).
  const attended = useRef(sessionId);
  attended.current = sessionId;

  // Defensive about the shape, deliberately: a shell older than this surface answers something else
  // entirely, and the console is the part of the drawer that may be missing. The RECORD above it is
  // what the drawer exists to show, and a console cannot be allowed to take it down.
  //
  // 🔴 MERGED by sequence, whichever source lands first (REV3) — as `mergeEvents` does for the
  // conversation. Filtered by "newer than the newest held", a live batch that beat the backlog's answer
  // moved that mark past the whole backlog, and it was thrown away without a word.
  const take = useCallback((tail: SessionTail | undefined) => {
    setLive(Boolean(tail?.live));
    setDropped(tail?.dropped ?? 0);
    setLines((held) => {
      const known = new Set(held.map((line) => line.sequence));
      const fresh = (tail?.lines ?? []).filter((line) => !known.has(line.sequence));
      if (fresh.length === 0) return held;
      const merged = [...held, ...fresh].sort((a, b) => a.sequence - b.sequence).slice(-CONSOLE_LINES);
      seen.current = Math.max(seen.current, merged[merged.length - 1]!.sequence);
      return merged;
    });
  }, []);

  useEffect(() => {
    seen.current = 0;
    setLines([]);
    setLive(false);
    setDropped(0);
    if (!isAvailable || !sessionId) return;

    let current = true;
    void getBridge()
      .invoke<SessionTail>('DAORIS.DRIVER', 'TAIL_SESSION', { payload: { id: sessionId } })
      .then((tail) => { if (current) take(tail); })
      // A console that failed to load is a quiet absence, not a toast: the record above it is the
      // thing the drawer exists to show, and it is already there.
      .catch(() => {});

    return () => { current = false; };
  }, [isAvailable, sessionId, take]);

  useShenoraEvent('DAORIS', 'SESSION_OUTPUT', (payload) => {
    const batch = payload as SessionTail | undefined;
    if (!sessionId || batch?.session !== sessionId || !batch.lines?.length) return;

    const first = batch.lines[0]!.sequence;
    if (seen.current > 0 && first > seen.current + 1) {
      // Something was missed. Ask for it rather than showing two halves as though they joined.
      void getBridge()
        .invoke<SessionTail>('DAORIS.DRIVER', 'TAIL_SESSION', { payload: { id: sessionId, after: seen.current } })
        // Only while this console is still that session's: the answer may land after a switch.
        .then((tail) => { if (attended.current === sessionId && tail?.session === sessionId) take(tail); })
        .catch(() => {});
      return;
    }

    // Live unless the batch says otherwise: a session's last words are written after it closed, and a
    // shell older than the flag says nothing (CONSOLE3c).
    take({ ...batch, live: batch.live !== false });
  });

  // An end is never a line, so a console asks again when one is told, and the answer says whether it
  // still runs: a stream's end as its session's streams changing (CONSOLE2c), and a session's own as
  // its ending. Found on the window twice (CONSOLE3a, 3c): a stopped task's console and a completed
  // chat's both still said live.
  const askAgain = () => {
    if (!sessionId) return;
    void getBridge()
      .invoke<SessionTail>('DAORIS.DRIVER', 'TAIL_SESSION', { payload: { id: sessionId, after: seen.current } })
      .then((tail) => { if (attended.current === sessionId && tail?.session === sessionId) take(tail); })
      .catch(() => {});
  };
  useShenoraEvent('DAORIS', 'SESSION_STREAMS', (payload) => {
    const session = (payload as { session?: string } | undefined)?.session;
    if (sessionId && session && sessionId.startsWith(`${session}/`)) askAgain();
  });
  useShenoraEvent('DAORIS', 'SESSION_ENDED', (payload) => {
    const session = (payload as { session?: string } | undefined)?.session;
    if (sessionId && session && (sessionId === session || sessionId.startsWith(`${session}/`))) askAgain();
  });

  return { lines, live, dropped };
}

/**
 * One thing a session runs beside itself (CONSOLE2): a subagent its harness spawned, or background
 * work it started, as a console stream of its own. `key` is what `useSessionConsole` tails it by.
 */
export type SessionStreamRow = {
  key: string;
  kind: string;
  name: string;
  live: boolean;
  /** How it ended, in the wire's word — null while it runs. */
  state: string | null;
  /** Whether a person can stop it now (CONSOLE3a): its harness said so, and it runs. */
  canStop?: boolean;
};

/**
 * A session's streams, oldest first (CONSOLE2).
 *
 * @remarks
 * Desktop-only for the console's reason (D47 §4). Asked for on open, and again whenever the driver
 * says one of this session's streams opened or ended (`SESSION_STREAMS`). Defensive about the shape,
 * as the console is: a shell older than this answers something else, and then the session simply has
 * no streams.
 */
export function useSessionStreams(sessionId: string | null): SessionStreamRow[] {
  const { isAvailable } = useShenora();
  const [streams, setStreams] = useState<SessionStreamRow[]>([]);
  const [asked, setAsked] = useState(0);
  const attended = useRef(sessionId);
  attended.current = sessionId;

  useEffect(() => setStreams([]), [sessionId]);

  useEffect(() => {
    if (!isAvailable || !sessionId) return;
    let current = true;
    void getBridge()
      .invoke<{ session?: string; streams?: unknown }>('DAORIS.DRIVER', 'SESSION_STREAMS', { payload: { id: sessionId } })
      .then((answer) => {
        if (!current || attended.current !== sessionId) return;
        const rows = Array.isArray(answer?.streams) ? answer.streams : [];
        setStreams(rows.filter((row): row is SessionStreamRow =>
          typeof row?.key === 'string' && typeof row?.name === 'string' && typeof row?.kind === 'string'));
      })
      .catch(() => {});
    return () => { current = false; };
  }, [isAvailable, sessionId, asked]);

  useShenoraEvent('DAORIS', 'SESSION_STREAMS', (payload) => {
    if (sessionId && (payload as { session?: string } | undefined)?.session === sessionId) setAsked((n) => n + 1);
  });

  return streams;
}

/**
 * Stop one task a session runs, from its tab (CONSOLE3a): the harness's own stop, sent by the session
 * that runs it. `stopped: false` is an answer — nothing here runs it any more, or its tool stopped
 * nothing — and the tab changes when the wire says how the task ended, not when this answers.
 */
export const useStopTask = () => useMutation({
  mutationFn: ({ id, key }: { id: string; key: string }) => call<{ stopped: boolean }>('STOP_TASK', { id, key }),
});
