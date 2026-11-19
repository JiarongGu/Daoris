import { useEffect, useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { readUxTrail, sendUxTrail, setUxTrail } from '../bridge/logs/uxTrail';
import { useAttention } from '../bridge/attention';
import type { PushedAttention } from '../work/attention/attentionPushed';

// The person's own UX trail:opt-in, off by default, kept on the desktop only (`UX_TRAIL`, the driver's `UxTrail`). It records
// the pages the person opens and the presses they make, a press named by its button's words and the *What needs you* row it is in, and
// when a row began waiting on them; never typing or a field's value. `tools/ux-trail.mjs` reads a trail: the time from a wait to its press.

/** One entry of the trail, the fields the desktop keeps and nothing else. */
export type TrailEntry =
  | { kind: 'start'; at: string; page: string }
  | { kind: 'page'; at: string; page: string; from: string }
  | { kind: 'press'; at: string; page: string; press: string; row?: string }
  | { kind: 'wait'; at: string; row: string; since?: string }
  | { kind: 'changed'; at: string; row: string }
  | { kind: 'cleared'; at: string; row: string };

/** What a press is: a control the person presses, never a field they type or choose in. */
const PRESSED = 'button,a,[role=button],[role=tab],[role=menuitem],[role=option],[role=link],summary';
const FIELD = 'input,textarea,select,[contenteditable=""],[contenteditable=true]';

const words = (element: Element): string =>
  (element.getAttribute('aria-label') || element.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 80);

/**
 * Records the presses and the pages of `doc` into `keep` until the returned stop: a press by its control's words and its row
 * (`data-attention-row`), a page as `page()` reads it, checked on a hash change and every `every` ms (null: only on a hash change).
 */
export function recordTrail({ doc, keep, page, now = () => new Date().toISOString(), every = 1000 }: {
  doc: Document; keep: (entry: TrailEntry) => void; page: () => string; now?: () => string; every?: number | null;
}): () => void {
  let last = page();
  keep({ kind: 'start', at: now(), page: last });
  const pressed = (event: Event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target || target.closest(FIELD)) return;
    const control = target.closest(PRESSED);
    if (!control) return;
    const row = control.closest('[data-attention-row]')?.getAttribute('data-attention-row') ?? undefined;
    keep({ kind: 'press', at: now(), page: last, press: words(control), ...(row ? { row } : {}) });
  };
  const moved = () => {
    const where = page();
    if (where === last) return;
    keep({ kind: 'page', at: now(), page: where, from: last });
    last = where;
  };
  doc.addEventListener('click', pressed, true);
  const view = doc.defaultView;
  view?.addEventListener('hashchange', moved);
  const timer = every === null ? null : setInterval(moved, every);
  return () => {
    doc.removeEventListener('click', pressed, true);
    view?.removeEventListener('hashchange', moved);
    if (timer) clearInterval(timer);
  };
}

/**
 * What the rows' move since `known` (each row's key to its question's mark) adds to the trail: a row that begins to wait, one whose
 * question changed (its wait began again, or a go-ahead went stale), one that cleared; and what is known now.
 */
export function trailOfRows(known: ReadonlyMap<string, string>, rows: readonly PushedAttention[], at: string): { entries: TrailEntry[]; known: Map<string, string> } {
  const entries: TrailEntry[] = [];
  const now = new Map<string, string>();
  for (const row of rows) {
    const key = `${row.kind}:${row.id}`;
    const said = `${row.since ?? ''}|${row.stale ? 'stale' : ''}`;
    now.set(key, said);
    const before = known.get(key);
    if (before === undefined) entries.push({ kind: 'wait', at, row: key, ...(row.since ? { since: row.since } : {}) });
    else if (before !== said) entries.push({ kind: 'changed', at, row: key });
  }
  for (const key of known.keys()) if (!now.has(key)) entries.push({ kind: 'cleared', at, row: key });
  return { entries, known: now };
}

const KEY = ['ux-trail'] as const;

/** Whether the trail is on, and the press that turns it on or off: Settings' row. */
export const useUxTrail = () => {
  const client = useQueryClient();
  const read = useQuery({ queryKey: KEY, queryFn: readUxTrail });
  const set = useMutation({
    mutationFn: setUxTrail,
    onSuccess: (on) => client.setQueryData(KEY, on),
  });
  return { on: read.data ?? false, set };
};

/** The window's recorder, while the trail is on: its entries sent every five seconds, and each row that begins waiting on the person. */
export function useUxTrailRecorder() {
  const { on } = useUxTrail();
  const attention = useAttention();
  const waiting = useRef(new Map<string, string>());
  const queue = useRef<TrailEntry[]>([]);
  useEffect(() => {
    if (!on) return undefined;
    const stop = recordTrail({ doc: document, keep: (entry) => queue.current.push(entry), page: () => location.pathname + location.search + location.hash });
    const flush = setInterval(() => {
      const entries = queue.current.splice(0);
      if (entries.length > 0) void sendUxTrail(entries).catch(() => {});
    }, 5000);
    return () => { stop(); clearInterval(flush); };
  }, [on]);
  const rows = attention.state === 'known' ? attention.rows : null;
  useEffect(() => {
    if (!on || !rows) return;
    const moved = trailOfRows(waiting.current, rows, new Date().toISOString());
    queue.current.push(...moved.entries);
    waiting.current = moved.known;
  }, [on, rows]);
}
