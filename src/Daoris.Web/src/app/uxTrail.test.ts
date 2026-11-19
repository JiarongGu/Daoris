// @vitest-environment jsdom
import { afterEach, describe, expect, it } from 'vitest';
import { recordTrail, trailOfRows, type TrailEntry } from './uxTrail';

// The UX trail's recorder:the pages the person opens and the presses they make, each press named by its button's words and
// the What needs you row it is in; never a field's value or typing.

describe('recordTrail', () => {
  let stop: (() => void) | null = null;
  afterEach(() => { stop?.(); stop = null; document.body.innerHTML = ''; });

  it('records a press by its words and its row, and nothing typed or chosen in a field', () => {
    const kept: TrailEntry[] = [];
    document.body.innerHTML = `
      <div data-attention-row="step:q7/sign"><button type="button"><span>Approve…</span></button></div>
      <input aria-label="why" value="my secret" />`;
    stop = recordTrail({ doc: document, keep: (entry) => kept.push(entry), page: () => '/sessions', now: () => '2026-11-16T09:51:00Z' });

    document.querySelector('span')!.click();
    document.querySelector('input')!.click();

    expect(kept.filter((entry) => entry.kind === 'press')).toEqual([
      { kind: 'press', at: '2026-11-16T09:51:00Z', page: '/sessions', press: 'Approve…', row: 'step:q7/sign' },
    ]);
    expect(JSON.stringify(kept)).not.toContain('secret');
  });

  it('records a page opened, with the page it came from', () => {
    const kept: TrailEntry[] = [];
    let where = '/overview';
    stop = recordTrail({ doc: document, keep: (entry) => kept.push(entry), page: () => where, now: () => 'now', every: null });

    where = '/sessions';
    window.dispatchEvent(new Event('hashchange'));

    expect(kept).toEqual([
      { kind: 'start', at: 'now', page: '/overview' },
      { kind: 'page', at: 'now', page: '/sessions', from: '/overview' },
    ]);
  });
});

describe('trailOfRows', () => {
  const row = (id: string, extra: Record<string, unknown> = {}) => ({ kind: 'go-ahead', id, since: '2026-11-16T09:00:00Z', ...extra }) as never;

  it('records a row that begins to wait, one whose question changed, and one that cleared', () => {
    const first = trailOfRows(new Map(), [row('a'), row('b')], 'now');
    expect(first.entries).toEqual([
      { kind: 'wait', at: 'now', row: 'go-ahead:a', since: '2026-11-16T09:00:00Z' },
      { kind: 'wait', at: 'now', row: 'go-ahead:b', since: '2026-11-16T09:00:00Z' },
    ]);

    const next = trailOfRows(first.known, [row('a', { stale: { since: null } }), row('b', { since: '2026-11-16T10:00:00Z' }), row('c')], 'later');
    expect(next.entries).toEqual([
      { kind: 'changed', at: 'later', row: 'go-ahead:a' },
      { kind: 'changed', at: 'later', row: 'go-ahead:b' },
      { kind: 'wait', at: 'later', row: 'go-ahead:c', since: '2026-11-16T09:00:00Z' },
    ]);

    const last = trailOfRows(next.known, [row('c')], 'end');
    expect(last.entries).toEqual([
      { kind: 'cleared', at: 'end', row: 'go-ahead:a' },
      { kind: 'cleared', at: 'end', row: 'go-ahead:b' },
    ]);
    expect(trailOfRows(last.known, [row('c')], 'same').entries).toEqual([]);
  });
});
