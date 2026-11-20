import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import { span } from '../../locales/format';
import { toTurns } from '../conversation/conversation';
import { ConversationView } from '../conversation/ConversationView';
import type { SessionEvent } from '../conversation/conversationTypes';
import { type BesideFocus, type BesideReader, lifetimesIn } from '../beside/besides';
import { SESSION } from '../beside/beside.fixtures';
import { LiveBand } from './LiveBand';

// A recorded session running a sub-agent and two background tasks, one of them quiet, its agent inside a call: the band reads the
// record's starts with no end, each child's own record, each task's console, and which tasks its door can stop.

const at = (minute: number, second = 0) => `2026-11-19T10:${String(minute).padStart(2, '0')}:${String(second).padStart(2, '0')}Z`;
const NOW = Date.parse(at(9));

const LIFETIMES: SessionEvent[] = [
  { seq: 3, at: at(0, 6), kind: 'child', id: 'a1', call: 'toolu_ag', depth: 1, name: 'find expiry tests', task: 'Find the tests', by: 'reported' },
  { seq: 5, at: at(0, 11), kind: 'task', id: 'b1', call: 'toolu_watch', name: 'npm test --watch', by: 'reported' },
  { seq: 6, at: at(1), kind: 'task', id: 'b2', call: 'toolu_build', name: 'npm run build', by: 'reported' },
  { seq: 7, at: at(1, 30), kind: 'child', id: 'a3', call: 'toolu_other', depth: 1, name: 'read the docs', by: 'reported' },
  { seq: 8, at: at(2), kind: 'child', id: 'a3', end: 'ok', by: 'reported' },
];

const CHILD: SessionEvent[] = [
  { seq: 1, at: at(0, 6), kind: 'user', origin: 'target', text: 'Find the tests' },
  { seq: 2, at: at(8, 30), kind: 'tool', id: 'toolu_read', title: 'Read test/auth.test.ts', toolKind: 'read', status: 'in_progress' },
];

const LINES: Readonly<Record<string, { lines: string[]; heard: number }>> = {
  b1: { lines: ['> vitest --watch', 'RUN v3', 'Tests: 41 passed, 0 failed', 'Watching for changes…'], heard: NOW - 10_000 },
  b2: { lines: ['building…'], heard: NOW - 7 * 60_000 },
};

const reader: BesideReader = {
  Child: ({ children }) => <>{children({ events: CHILD, loaded: true })}</>,
  Tail: ({ task, children }) => <>{children(LINES[task]?.lines ?? [], LINES[task]?.heard ?? null)}</>,
  Stream: ({ task }) => <pre data-stream={task}>{(LINES[task]?.lines ?? []).join('\n')}</pre>,
};

const band = (props: Partial<Parameters<typeof LiveBand>[0]> = {}) => {
  const onStop = vi.fn();
  const onOpen = vi.fn();
  render(
    <LiveBand
      lifetimes={LIFETIMES} act={{ title: 'Edit src/auth.ts', since: at(8, 50) }} reader={reader} stoppable={new Set(['b1'])}
      onStop={onStop} onOpen={onOpen} {...props}
    />,
  );
  return { onStop, onOpen };
};

const row = (id: string) => document.querySelector<HTMLElement>(`[data-band-row="${id}"]`)!;
const rows = () => [...document.querySelectorAll('[data-band-row]')].map((each) => each.getAttribute('data-band-row'));

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] });
  vi.setSystemTime(NOW);
});
afterEach(() => vi.useRealTimers());

describe('the live band', () => {
  it('draws the agent\'s act and each running sub-agent and task, its time ticking from its start, its last line', () => {
    band();

    expect(screen.getByRole('region', { name: 'Running 4' })).toBeTruthy();
    expect(rows()).toEqual(['act', 'a1', 'b1', 'b2']);
    expect(row('act').textContent).toContain('Edit src/auth.ts');
    expect(row('act').textContent).toContain(span(10_000));
    expect(row('a1').textContent).toContain('find expiry tests');
    expect(row('a1').textContent).toContain(span(NOW - Date.parse(at(0, 6))));
    expect(row('a1').textContent).toContain('Read test/auth.test.ts');
    expect(row('b1').textContent).toContain('npm test --watch');
    expect(row('b1').textContent).toContain('Watching for changes…');

    act(() => { vi.advanceTimersByTime(60_000); });
    expect(row('a1').textContent).toContain(span(NOW + 60_000 - Date.parse(at(0, 6))));
  });

  it('marks one with no output past six minutes, and no other', () => {
    band();

    expect(row('b2').textContent).toContain(`no output ${span(7 * 60_000)}`);
    expect(row('b1').textContent).not.toContain('no output');
    expect(row('a1').textContent).not.toContain('no output');
  });

  it('stops a task its door can stop, asked once in place and stopping until its end is read; elsewhere its tip says why', () => {
    const { onStop } = band();

    fireEvent.click(within(row('b1')).getByRole('button', { name: 'Stop npm test --watch' }));
    expect(onStop).not.toHaveBeenCalled();
    expect(row('b1').textContent).toContain('Its output ends with it.');
    fireEvent.click(within(row('b1')).getByRole('button', { name: 'Stop it' }));

    expect(onStop).toHaveBeenCalledWith('b1', 'npm test --watch');
    expect(row('b1').textContent).toContain('stopping');
    expect(within(row('b1')).queryByRole('button', { name: /^Stop/ })).toBeNull();

    expect(within(row('b2')).queryByRole('button', { name: /^Stop/ })).toBeNull();
    expect(row('b2').querySelector('[data-why]')?.getAttribute('title')).toBe(
      'Its agent cannot stop this task from here: only a task its agent marks as stoppable can be.');
    expect(row('a1').querySelector('[data-why]')?.getAttribute('title')).toBe('No agent stops a subagent alone: it ends with its session.');
    expect(row('b1').querySelector('[data-why]')).toBeNull();
  });

  it('follows a three-line tail and opens a sub-agent or a task in the panel\'s focus', () => {
    const { onOpen } = band();

    fireEvent.click(within(row('b1')).getByRole('button', { name: 'Follow' }));
    expect(row('b1').querySelector('pre')?.textContent).toBe('RUN v3\nTests: 41 passed, 0 failed\nWatching for changes…');

    fireEvent.click(within(row('a1')).getByRole('button', { name: 'Open' }));
    expect(onOpen).toHaveBeenLastCalledWith({ child: ['a1'] });
    fireEvent.click(within(row('b1')).getByRole('button', { name: 'Open' }));
    expect(onOpen).toHaveBeenLastCalledWith({ task: 'b1' });
  });

  it('draws a sub-agent another started under its parent, indented one past it, and opens it by the way to it', () => {
    const nested: SessionEvent[] = [
      ...LIFETIMES,
      { seq: 9, at: at(3), kind: 'child', id: 'a4', parent: 'a1', name: 'read fixtures', by: 'reported' },
      { seq: 10, at: at(4), kind: 'child', id: 'a5', parent: 'a4', depth: 3, name: 'grep names', by: 'reported' },
    ];
    const { onOpen } = band({ lifetimes: nested });

    expect(rows()).toEqual(['act', 'a1', 'a4', 'a5', 'b1', 'b2']);
    expect(row('a1').style.marginLeft).toBe('');
    expect(row('a4').style.marginLeft).toBe('16px');
    expect(row('a5').style.marginLeft).toBe('32px');
    fireEvent.click(within(row('a5')).getByRole('button', { name: 'Open' }));
    expect(onOpen).toHaveBeenLastCalledWith({ child: ['a1', 'a4', 'a5'] });
  });

  it('says nothing runs where nothing does', () => {
    band({ lifetimes: LIFETIMES.slice(3), act: null });

    expect(rows()).toEqual([]);
    expect(screen.getByRole('region', { name: 'Nothing running' }).textContent).toBe('Nothing running');
  });

  it('folds to a count in a short window, a press away from its lines', () => {
    band({ short: true });

    const count = screen.getByRole('button', { name: 'Running 4' });
    expect(count.getAttribute('aria-expanded')).toBe('false');
    expect(rows()).toEqual([]);
    fireEvent.click(count);
    expect(rows()).toEqual(['act', 'a1', 'b1', 'b2']);
  });

  it('draws a helper or a reviewer working for the line, its time ticking from when it was asked, its session a press away', () => {
    const onOpenSession = vi.fn();
    band({
      lifetimes: [], act: null, onOpenSession,
      exchanges: [{
        id: 'helper:h1', part: 's1', expects: 'answer', to: 'codex', delivery: 'taken', outcome: 'asked', holds: true, at: at(7),
        session: 'h-sess', agent: 'codex',
      }],
    });

    expect(screen.getByRole('region', { name: 'Running 1' })).toBeTruthy();
    expect(rows()).toEqual(['helper:h1']);
    expect(row('helper:h1').textContent).toContain('codex, as helper');
    expect(row('helper:h1').textContent).toContain(span(2 * 60_000));
    fireEvent.click(within(row('helper:h1')).getByRole('button', { name: 'Open' }));
    expect(onOpenSession).toHaveBeenCalledWith('h-sess');
  });
});

describe('what the band opens and keeps', () => {
  // The recorded session of the timeline's own tests, its watch task stopped at its end.
  const view = (events: SessionEvent[], focus: BesideFocus | null, onFocus = vi.fn()) => {
    render(
      <Tooltip.Provider>
        <ConversationView turns={toTurns(events).turns} beside={{ reader, lifetimes: lifetimesIn(events), focus, onFocus }} />
      </Tooltip.Provider>,
    );
    return onFocus;
  };

  it('puts a task in the panel\'s focus: its whole console, a breadcrumb back', () => {
    const onFocus = view(SESSION, { task: 'b1' });
    const where = screen.getByRole('navigation', { name: 'Where in the session' });

    expect(within(where).getAllByRole('button').map((each) => each.textContent)).toEqual(['Session', 'npm test --watch']);
    expect(document.querySelector('[data-stream="b1"]')?.textContent).toContain('> vitest --watch');
    fireEvent.click(within(where).getByRole('button', { name: 'Session' }));
    expect(onFocus).toHaveBeenCalledWith(null);
  });

  it('keeps who stopped a task on its row where it ended', () => {
    view(SESSION.map((event) => (event.kind === 'task' && event.end ? { ...event, origin: 'person' } : event)), null);

    expect(document.querySelector('[data-beside-end="b1"]')?.textContent).toContain('stopped by you');
  });
});
