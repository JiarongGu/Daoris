import { describe, expect, it, vi } from 'vitest';
import { type Command, commands, matching } from './commands';

// The registry is the half that matters (SURF9): "what can I do right now" is a value, so every
// world — a browser, a shell in Manage, a shell in Work — is an argument rather than an arrangement.

const world = (over: Partial<Parameters<typeof commands>[0]> = {}) => ({
  label: (id: string) => id,
  group: (id: string) => id,
  attached: true,
  mode: 'manage' as const,
  waiting: 0,
  go: vi.fn(),
  setMode: vi.fn(),
  refresh: vi.fn(),
  toggleLanguage: vi.fn(),
  startSession: vi.fn(),
  review: vi.fn(),
  monitor: vi.fn(),
  ...over,
});

const ids = (list: Command[]) => list.map((command) => command.id);

describe('the command registry', () => {
  it('offers every management domain, and the global actions, in a browser', () => {
    const list = ids(commands(world({ attached: false })));

    expect(list).toContain('go.overview');
    expect(list).toContain('go.search');
    expect(list).toContain('do.refresh');
    expect(list).toContain('do.language');
  });

  /**
   * 🔴 The disclosure boundary, enforced by OMISSION (D47 §4). A palette is a promise that what it
   * lists can be done — so a shell-only action listed in a browser would be the palette lying, and
   * "present but disabled" would be the same lie with extra steps.
   */
  it('offers nothing shell-only in a browser — absent, never disabled', () => {
    const list = ids(commands(world({ attached: false })));

    expect(list).not.toContain('go.settings');
    expect(list).not.toContain('go.work');
    expect(list).not.toContain('go.manage');
    expect(list).not.toContain('work.start');
    expect(list).not.toContain('work.review');
    // Nothing sneaks in under another name either.
    expect(list.some((id) => id.startsWith('work.'))).toBe(false);
  });

  it('offers the machine and the session actions where a shell is here', () => {
    const list = ids(commands(world()));

    expect(list).toContain('go.settings');
    expect(list).toContain('work.start');
    expect(list).toContain('work.review');
    // The second screen (SURF8) — a window is the shell's to open, so it follows the same rule.
    expect(list).toContain('work.monitor');
  });

  /**
   * Detach is absent when nothing is attended, for the same reason a shell-only action is absent in
   * a browser: a row that does nothing is a palette breaking its promise.
   */
  it('offers detaching only where there is a session to detach', () => {
    expect(ids(commands(world()))).not.toContain('work.detach');

    const detach = vi.fn();
    const list = commands(world({ detach }));
    expect(ids(list)).toContain('work.detach');

    list.find((command) => command.id === 'work.detach')!.run();
    expect(detach).toHaveBeenCalled();
  });

  /** Only ever the frame you are not in: an entry that does nothing is noise in this list. */
  it('offers the other frame, never the one you are in', () => {
    expect(ids(commands(world({ mode: 'manage' })))).toContain('go.work');
    expect(ids(commands(world({ mode: 'manage' })))).not.toContain('go.manage');

    expect(ids(commands(world({ mode: 'work' })))).toContain('go.manage');
    expect(ids(commands(world({ mode: 'work' })))).not.toContain('go.work');
  });

  it('runs what it was handed, and nothing else', () => {
    const go = vi.fn();
    const setMode = vi.fn();
    const list = commands(world({ go, setMode }));

    list.find((command) => command.id === 'go.quests')!.run();
    expect(go).toHaveBeenCalledWith('quests');

    list.find((command) => command.id === 'go.work')!.run();
    expect(setMode).toHaveBeenCalledWith('work');
  });

  it('gives every command a stable id and a distinct one', () => {
    const list = ids(commands(world()));
    expect(new Set(list).size).toBe(list.length);
  });
});

describe('matching what was typed', () => {
  const list = commands(world({
    label: (id: string) => ({
      'go.overview': 'Overview',
      'go.quests': 'Quests',
      'go.projects': 'Projects',
      'go.convergence': 'Convergence',
      'go.search': 'Search',
      'go.settings': 'Machine',
      'go.work': 'Work',
      'work.start': 'Start a session',
      'work.review': 'Review what landed',
      'do.refresh': 'Refresh the index',
      'do.language': '中文',
    })[id] ?? id,
  }));

  it('shows everything when nothing is typed — a palette is not a filter until you type', () => {
    expect(matching(list, '')).toHaveLength(list.length);
    expect(matching(list, '   ')).toHaveLength(list.length);
  });

  it('matches a subsequence, not only a substring', () => {
    // "cnv" is how a person types Convergence when they are not looking at the list.
    expect(matching(list, 'cnv').map((c) => c.id)).toContain('go.convergence');
    expect(matching(list, 'sas').map((c) => c.id)).toContain('work.start');
  });

  it('ranks a word-boundary match above one buried mid-word', () => {
    // "se" begins Search; it appears mid-word in Convergence and "a session".
    expect(matching(list, 'se')[0].id).toBe('go.search');
  });

  it('finds a command by a keyword it does not show', () => {
    // "settings" is what the Machine view used to be called, and what people will type.
    expect(matching(list, 'settings').map((c) => c.id)).toContain('go.settings');
    expect(matching(list, 'diff').map((c) => c.id)).toContain('work.review');
  });

  it('matches 中文 — the console is bilingual and so is the palette', () => {
    expect(matching(list, '中文').map((c) => c.id)).toContain('do.language');
  });

  it('answers nothing for a needle that is in nothing, rather than everything', () => {
    expect(matching(list, 'zzzzq')).toEqual([]);
  });
});
