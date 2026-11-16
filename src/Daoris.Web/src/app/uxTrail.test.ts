// @vitest-environment jsdom
import { afterEach, describe, expect, it } from 'vitest';
import { recordTrail, type TrailEntry } from './uxTrail';

// The UX trail's recorder (UXTRAIL1): the pages the person opens and the presses they make, each press named by its button's words and
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
