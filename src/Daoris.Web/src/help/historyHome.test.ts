import { describe, expect, it } from 'vitest';
import { type HelpConversationRow } from './history';
import { homeGroups, showingKey, strayFocus } from './historyHome';

// The history by home workspace (`docs/2026-11-11-ask-scope-ux-design.md` §3): the window's workspace first, the others by name,
// each group in the driver's order; and a row's focus chip only where its focus is not its home.

const row = (session: string, workspace: string | undefined, over: Partial<HelpConversationRow> = {}): HelpConversationRow => ({
  session, title: session, name: null, opening: session, about: null, created: '2026-10-08T10:00:00Z', last: '2026-10-08T10:00:00Z',
  pinned: null, live: false, resumable: true, from: null, handed: null, found: null, foundLine: null, workspace, ...over,
});

describe('the history grouped by home', () => {
  const rows = [row('a', 'work'), row('b', 'games'), row('c', undefined), row('d', 'Games'), row('e', 'alpha')];

  it('puts the window’s workspace first, marked, then the others by name, each in the order given', () => {
    const groups = homeGroups(rows, 'games');
    expect(groups.map((group) => [group.home, group.here, group.rows.map((each) => each.session)])).toEqual([
      ['games', true, ['b', 'd']],
      ['alpha', false, ['e']],
      ['default', false, ['c']],
      ['work', false, ['a']],
    ]);
  });

  it('marks none in a window on every workspace', () => {
    expect(homeGroups(rows, null).map((group) => group.here)).toEqual([false, false, false, false]);
    expect(homeGroups(rows, null).map((group) => group.home)).toEqual(['alpha', 'default', 'games', 'work']);
  });

  it('is empty for no rows', () => {
    expect(homeGroups([], 'games')).toEqual([]);
  });
});

describe('a row’s focus chip', () => {
  it('shows the focus only where it is not the home', () => {
    expect(strayFocus(row('a', 'games'))).toBeNull();
    expect(strayFocus(row('a', 'games', { focus: { kind: 'workspace', workspace: 'Games' } }))).toBeNull();
    expect(strayFocus(row('a', undefined, { focus: { kind: 'machine' } }))).toBeNull();
    const engine = { kind: 'repository', workspace: 'games', repository: 'engine' } as const;
    expect(strayFocus(row('a', 'games', { focus: engine }))).toEqual(engine);
    expect(strayFocus(row('a', 'games', { focus: { kind: 'workspace', workspace: 'work' } }))).toEqual({ kind: 'workspace', workspace: 'work' });
    expect(strayFocus(row('a', 'games', { focus: { kind: 'machine' } }))).toEqual({ kind: 'machine' });
  });
});

describe('a filter’s key', () => {
  it('is one per filter, a place by its spelling', () => {
    expect(showingKey({ kind: 'here' })).toBe('here');
    expect(showingKey({ kind: 'all' })).toBe('all');
    expect(showingKey({ kind: 'about', place: { kind: 'repository', workspace: 'games', repository: 'engine' } })).toBe('about:games/engine');
  });
});
