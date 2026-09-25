import { describe, expect, it } from 'vitest';
import type { Session } from '../api';
import { byName, marked, readable } from './railSearch';

// Searching the rail (RAIL1): by name at once, on the page — the derived title, the repository and the
// id — and by content over the bridge. Pure, so every query is an ordinary assertion.

const session = (id: string, repository: string, over: Partial<Session> = {}): Session => ({
  id, repository, adapter: 'claude-code', state: 'working', kind: 'chat',
  created: '2026-09-26T09:00:00Z', updated: '2026-09-26T09:00:00Z', ...over,
});

describe('searching by name', () => {
  const sessions = [
    session('c0ffee11', 'engine'),
    session('d4e5f6a7', 'game'),
    session('a1b2c3d4', 'tools', { state: 'completed' }),
  ];
  const titles: Record<string, string> = {
    c0ffee11: 'Cap the hydration per frame',
    d4e5f6a7: 'Read README.md and tell me its first heading',
    a1b2c3d4: 'conversation',
  };
  const title = (s: Session) => titles[s.id]!;

  it('finds a session by its title, its repository or its id, whatever the case', () => {
    expect(byName(sessions, 'HYDRATION', title).map((s) => s.id)).toEqual(['c0ffee11']);
    expect(byName(sessions, 'game', title).map((s) => s.id)).toEqual(['d4e5f6a7']);
    expect(byName(sessions, 'a1b2', title).map((s) => s.id)).toEqual(['a1b2c3d4']);
  });

  it('keeps the order it was given, and finds nothing for nothing typed', () => {
    expect(byName(sessions, 'e', title).map((s) => s.id)).toEqual(['c0ffee11', 'd4e5f6a7', 'a1b2c3d4']);
    expect(byName(sessions, '   ', title)).toEqual([]);
  });
});

/**
 * Seen on the window (RAIL1): an agent writes Markdown, and a snippet showed `**heliotrope**` and its
 * backticks as written. A snippet is read as a line, so the marks that only style it go; an underscore
 * stays, since `__init__` is a name and not emphasis.
 */
describe('a snippet read as a line', () => {
  it('drops the marks that only style the words', () => {
    expect(readable('The word is **heliotrope**. It is the only line in `design notes.md`.'))
      .toBe('The word is heliotrope. It is the only line in design notes.md.');
    expect(readable('call `__init__` twice')).toBe('call __init__ twice');
  });
});

describe('marking what matched', () => {
  it('splits a snippet around every match, whatever the case', () => {
    expect(marked('The cap belongs in the Streamer, not the streamer’s loader.', 'streamer')).toEqual([
      { text: 'The cap belongs in the ', match: false },
      { text: 'Streamer', match: true },
      { text: ', not the ', match: false },
      { text: 'streamer', match: true },
      { text: '’s loader.', match: false },
    ]);
  });

  it('marks a 中文 match, and leaves a snippet with none whole', () => {
    expect(marked('把流式加载的上限做成可配置的', '流式加载')).toEqual([
      { text: '把', match: false },
      { text: '流式加载', match: true },
      { text: '的上限做成可配置的', match: false },
    ]);
    expect(marked('nothing here', 'budget')).toEqual([{ text: 'nothing here', match: false }]);
  });
});
