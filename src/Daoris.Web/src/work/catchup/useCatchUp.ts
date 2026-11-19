import { type RefObject, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useMoveReadMark, useReadMark } from '../../bridge/shell';
import type { SessionEvent, Turn } from '../conversation/conversationTypes';
import type { Exchanges } from '../conversation/exchanges';
import { type Digest, digestOf, type StepsNow } from './digest';
import { isForward, type MarkPlace, newestShown, type ReadMark } from './readMark';

/** What the attended part's host knows of the conversation beyond its record: its line, what waits on the person, its run's steps. */
export type CatchUpFacts = { line: string; waiting: number; steps?: StepsNow | null };

/** How long the mark waits after a scroll before it reads what is on screen. */
const SETTLE = 800;

/**
 * Catch-up for the attended part of a conversation: the person's read mark as the conversation opened (read once per opening, so
 * the digest holds still while they read), *Since you left* computed at it, the panel placed at it once, and the mark moved to the
 * newest event on screen while the window has focus, only forward. Nothing moves the mark before the panel is placed, so the tail
 * the page opens at is never taken for read.
 */
export function useCatchUp({ facts, session, scroller, events, turns, where, lifetimes, exchanges, loaded, earlier, loadEarlier }: {
  facts?: CatchUpFacts;
  session: string;
  scroller: RefObject<HTMLElement | null>;
  events: readonly SessionEvent[];
  turns: readonly Turn[];
  where: Readonly<Record<number, string>>;
  lifetimes: readonly SessionEvent[];
  exchanges: Exchanges;
  loaded: boolean;
  earlier: boolean;
  loadEarlier: () => unknown;
}): { digest: Digest | null; read?: string } {
  const line = facts?.line ?? null;
  const { mark } = useReadMark(line);
  const { mutate: move } = useMoveReadMark();
  const visit = `${line ?? ''}:${session}`;
  const [opening, setOpening] = useState<{ visit: string; mark: ReadMark | null } | null>(null);
  const held = opening?.visit === visit ? opening.mark : undefined;
  useEffect(() => {
    if (line && mark !== undefined && opening?.visit !== visit) setOpening({ visit, mark });
  }, [line, mark, opening, visit]);

  // The mark's own event must be held for the digest to start at it: the pages before are read until it is, or none are left.
  const first = events[0]?.seq;
  useEffect(() => {
    if (held && held.part === session && first !== undefined && first > held.seq + 1 && earlier) void loadEarlier();
  }, [held, session, first, earlier, loadEarlier]);

  const digest = useMemo(
    () => (held && loaded ? digestOf({ turns, part: session, mark: held, lifetimes, exchanges, waiting: facts?.waiting, steps: facts?.steps }) : null),
    [held, loaded, turns, session, lifetimes, exchanges, facts?.waiting, facts?.steps],
  );

  // Placed once per opening: at the mark's row where there is one, else where the page opened (its tail, following).
  const [placed, setPlaced] = useState<string | null>(null);
  const settled = held !== undefined && loaded;
  useEffect(() => {
    if (!settled || placed === visit) return undefined;
    const frame = requestAnimationFrame(() => {
      if (digest) scroller.current?.querySelector('[data-catchup]')?.scrollIntoView({ block: 'start' });
      setPlaced(visit);
    });
    return () => cancelAnimationFrame(frame);
  }, [settled, placed, visit, digest, scroller]);

  // What was last sent, so a mark is sent only forward of it.
  const sent = useRef<MarkPlace | null>(null);
  useEffect(() => { sent.current = held ?? null; }, [held]);
  const timeOf = useCallback((seq: number) => events.find((event) => event.seq === seq)?.at, [events]);
  const passed = facts?.steps?.passed;
  const read = useCallback(() => {
    const box = scroller.current;
    if (!line || !box || placed !== visit || !document.hasFocus() || document.visibilityState !== 'visible') return;
    const edge = box.getBoundingClientRect();
    const root = box.querySelector(`[data-part="${CSS.escape(session)}"]`) ?? box;
    const shown = [...root.querySelectorAll<HTMLElement>('[data-block]')]
      .filter((block) => !block.closest('[data-child]'))
      .filter((block) => {
        const at = block.getBoundingClientRect();
        return at.bottom > edge.top && at.top < edge.bottom;
      })
      .map((block) => block.dataset.block ?? '');
    const newest = newestShown(shown, where, timeOf);
    if (!newest) return;
    const next = { part: session, ...newest };
    if (!isForward(sent.current, next)) return;
    sent.current = next;
    move({ conversation: line, ...next, steps: passed ?? null });
  }, [line, scroller, placed, visit, session, where, timeOf, passed, move]);

  // Read again a moment after the person scrolls, when the window takes focus, and as the conversation grows under their eyes.
  useEffect(() => {
    const box = scroller.current;
    if (!box || placed !== visit) return undefined;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const soon = () => {
      clearTimeout(timer);
      timer = setTimeout(read, SETTLE);
    };
    soon();
    box.addEventListener('scroll', soon, { passive: true });
    window.addEventListener('focus', soon);
    return () => {
      clearTimeout(timer);
      box.removeEventListener('scroll', soon);
      window.removeEventListener('focus', soon);
    };
  }, [scroller, placed, visit, read]);

  return { digest, read: held?.read };
}
