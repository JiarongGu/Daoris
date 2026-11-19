import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import * as Tooltip from '@radix-ui/react-tooltip';
import '../../locales/i18n';
import { span } from '../../locales/format';
import { toTurns } from '../conversation/conversation';
import { ConversationView } from '../conversation/ConversationView';
import { type BesideReader, lifetimesIn } from './besides';
import { RECORDS, SESSION } from './beside.fixtures';

// A recorded session whose subagent spawned another, and a watch task, drawn in the session's one timeline.

const reader: BesideReader = {
  Child: ({ child, children }) => <>{children({ events: RECORDS[child] ?? null, loaded: true })}</>,
  Tail: ({ children }) => <>{children(['Tests: 41 passed, 0 failed', 'Watching for changes…'])}</>,
};

const view = (with_: BesideReader, onShow?: (key: string) => void) => render(
  <Tooltip.Provider>
    <ConversationView turns={toTurns(SESSION).turns} onShow={onShow} beside={{ reader: with_, lifetimes: lifetimesIn(SESSION) }} />
  </Tooltip.Provider>);

const draw = () => {
  const onShow = vi.fn();
  view(reader, onShow);
  return onShow;
};

const crumbs = () => within(screen.getByRole('navigation', { name: 'Where in the session' })).getAllByRole('button').map((each) => each.textContent);

describe('a subagent under the call that spawned it', () => {
  it('is a row saying its task, how it stands, its calls, how long it ran and its closing words', () => {
    draw();
    const row = screen.getByRole('button', { name: /find expiry tests/ });
    const said = row.closest('[data-beside-child]')!;

    expect(said.getAttribute('data-beside-child')).toBe('a1');
    expect(row.textContent).toContain('subagent');
    expect(row.textContent).toContain('finished');
    expect(row.textContent).toContain('2 calls');
    expect(row.textContent).toContain(span(174_000));
    expect(said.textContent).toContain('It found two tests that cover expiry.');
    expect(row.getAttribute('title')).toBe('Find the tests that cover token expiry');
  });

  it('opens in place to its own turns one level in, where the subagent it spawned is a row that takes the focus', () => {
    draw();
    fireEvent.click(screen.getByRole('button', { name: /find expiry tests/ }));
    const inside = document.querySelector<HTMLElement>('[data-child="a1"]')!;

    expect(inside.getAttribute('data-depth')).toBe('1');
    expect(within(inside).getByText('Read test/auth.test.ts')).toBeTruthy();

    fireEvent.click(within(inside).getByRole('button', { name: /search the fixtures/ }));

    // The deeper one is the panel's focus: its own timeline, the session's out of view, a breadcrumb back through its parent.
    expect(crumbs()).toEqual(['Session', 'find expiry tests', 'search the fixtures']);
    expect(screen.getByText('Search "expir"')).toBeTruthy();
    expect(screen.queryByText('also cover the expiry path')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'find expiry tests' }));
    expect(crumbs()).toEqual(['Session', 'find expiry tests']);
    expect(screen.getByText('Read test/auth.test.ts')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Session' }));
    expect(screen.queryByRole('navigation', { name: 'Where in the session' })).toBeNull();
    expect(screen.getByText('also cover the expiry path')).toBeTruthy();
  });

  it('says its own record is not kept where none is, never an empty run', () => {
    const none: BesideReader = { Child: ({ children }) => <>{children({ events: null, loaded: true })}</> };
    view(none);
    fireEvent.click(screen.getByRole('button', { name: /find expiry tests/ }));

    expect(document.querySelector('[data-child="a1"]')?.textContent).toBe('Its own record is not kept on this machine.');
  });
});

describe('a background task', () => {
  it('is a row where it started and one where it ended, each a press to the other', () => {
    const onShow = draw();
    const start = document.querySelector<HTMLElement>('[data-beside-start="b1"]')!;
    const end = document.querySelector<HTMLElement>('[data-beside-end="b1"]')!;

    expect(start.textContent).toContain('background task');
    expect(start.textContent).toContain('stopped');
    expect(end.textContent).toContain('npm test --watch');
    expect(end.textContent).toContain('stopped');
    expect(end.textContent).toContain(span(719_000));
    expect(end.textContent).toContain('Watching for changes…');

    fireEvent.click(within(start).getByRole('button', { name: 'To where it ended' }));
    expect(onShow).toHaveBeenLastCalledWith(end.closest('[data-block]')!.getAttribute('data-block'));
    fireEvent.click(within(end).getByRole('button', { name: 'To where it started' }));
    expect(onShow).toHaveBeenLastCalledWith('e4');
  });
});
