import { describe, expect, it, vi } from 'vitest';
import { type Command, commands, matching } from './commands';

// The registry is the half that matters (SURF9): "what can I do right now" is a value, so every
// world — a browser, a shell on Overview, a shell on Sessions — is an argument rather than an
// arrangement.

const world = (over: Partial<Parameters<typeof commands>[0]> = {}): Parameters<typeof commands>[0] => ({
  label: (id: string) => id,
  group: (id: string) => id,
  attached: true,
  current: 'overview',
  go: vi.fn(),
  refresh: vi.fn(),
  toggleLanguage: vi.fn(),
  startSession: vi.fn(),
  review: vi.fn(),
  monitor: vi.fn(),
  browser: vi.fn(),
  help: vi.fn(),
  quickAsk: vi.fn(),
  ask: vi.fn(),
  ...over,
});

const ids = (list: Command[]) => list.map((command) => command.id);

describe('the command registry', () => {
  it('offers every view a browser has, and the global actions', () => {
    const list = ids(commands(world({ attached: false, current: 'quests' })));

    expect(list).toContain('go.overview');
    expect(list).toContain('go.search');
    // Settings is everywhere since D66: a browser has appearance to set, if nothing of a machine.
    expect(list).toContain('go.settings');
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

    expect(list).not.toContain('go.sessions');
    expect(list).not.toContain('work.start');
    expect(list).not.toContain('work.review');
    // Nothing sneaks in under another name either.
    expect(list.some((id) => id.startsWith('work.'))).toBe(false);
  });

  /**
   * An ask (INT4c) is a local host's HTTP door, so a browser on this machine can make one as well as
   * the shell can — offered from anywhere, whatever view is in front, even Quests where its button is.
   */
  it('offers asking the circle everywhere, a browser included', () => {
    const run = vi.fn();
    const browser = commands(world({ attached: false, current: 'quests', ask: run }));

    const ask = browser.find((command) => command.id === 'do.ask');
    expect(ask).toBeDefined();
    ask!.run();
    expect(run).toHaveBeenCalledOnce();
    expect(ids(commands(world()))).toContain('do.ask');
  });

  it('offers Sessions and the session actions where a shell is here', () => {
    const list = ids(commands(world()));

    expect(list).toContain('go.sessions');
    expect(list).toContain('go.settings');
    expect(list).toContain('work.start');
    expect(list).toContain('work.review');
    // The second screen (SURF8) — a window is the shell's to open, so it follows the same rule.
    expect(list).toContain('work.monitor');
    // Daoris's own browser (D78) — a window of the shell, by the same rule.
    expect(list).toContain('work.browser');
    // Ask Daoris, in its region and in the palette's box (DOCK1d): the machine's, so the shell's.
    expect(list).toContain('work.help');
    expect(list).toContain('work.quickAsk');
  });

  /**
   * One navigation, one list (D66): there is no mode to switch, and the palette never offers the view
   * already in front of the person — an entry that does nothing is noise in this list.
   */
  it('never offers the view you are already on', () => {
    expect(ids(commands(world({ current: 'sessions' })))).not.toContain('go.sessions');
    expect(ids(commands(world({ current: 'sessions' })))).toContain('go.overview');
    expect(ids(commands(world({ current: 'overview' })))).not.toContain('go.overview');
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

  it('runs what it was handed, and nothing else', () => {
    const go = vi.fn();
    const list = commands(world({ go }));

    list.find((command) => command.id === 'go.quests')!.run();
    list.find((command) => command.id === 'go.sessions')!.run();
    expect(go.mock.calls).toEqual([['quests'], ['sessions']]);
  });

  it('gives every command a stable id and a distinct one', () => {
    const list = ids(commands(world()));
    expect(new Set(list).size).toBe(list.length);
  });
});

describe('matching what was typed', () => {
  const list = commands(world({
    current: 'quests',
    label: (id: string) => ({
      'go.overview': 'Overview',
      'go.sessions': 'Sessions',
      'go.projects': 'Projects',
      'go.convergence': 'Convergence',
      'go.search': 'Search',
      'go.settings': 'Settings',
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
    // "sea" begins Search, and is buried mid-word nowhere else.
    expect(matching(list, 'sea')[0].id).toBe('go.search');
  });

  it('finds a command by a keyword it does not show', () => {
    // "theme" and "machine" are what people will type for Settings (D66); neither is in its title.
    expect(matching(list, 'theme').map((c) => c.id)).toContain('go.settings');
    expect(matching(list, 'machine').map((c) => c.id)).toContain('go.settings');
    expect(matching(list, 'diff').map((c) => c.id)).toContain('work.review');
  });

  it('matches 中文 — the console is bilingual and so is the palette', () => {
    expect(matching(list, '中文').map((c) => c.id)).toContain('do.language');
  });

  it('answers nothing for a needle that is in nothing, rather than everything', () => {
    expect(matching(list, 'zzzzq')).toEqual([]);
  });
});
