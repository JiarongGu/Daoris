import { describe, expect, it } from 'vitest';
import type { Session } from '../api';
import { VIEWS } from '../commands';
import type { KnowledgeMode } from '../knowledge/modes';
import { en } from '../locales';
import { PLACE_DOMAINS, PLACE_PARTS } from './places';
import { attendedOf, type HelpWhere, itemOf, prefaceOf } from './where';

// HELP1b (D89): where the person is, as the helper is told it ahead of their words.

describe('where the person is', () => {
  it('names the view and the workspace in scope', () => {
    expect(prefaceOf({ view: 'overview', workspace: 'aurora' }))
      .toBe('Where the person is now: the Overview view, workspace `aurora`.');
    expect(prefaceOf({ view: 'quests', workspace: null }))
      .toBe('Where the person is now: the Quests view, every workspace.');
  });

  it('names the settings domain on Settings', () => {
    expect(prefaceOf({ view: 'settings', workspace: null, settings: 'driver' }))
      .toBe('Where the person is now: Settings → Driver, every workspace.');
    // UX6j (D150 §2.3): the guide is Get started again, since Setup names a repository's and a workspace's tab.
    expect(prefaceOf({ view: 'settings', workspace: null, settings: 'start' }))
      .toBe('Where the person is now: Settings → Get started, every workspace.');
  });

  /**
   * NAME1b: the agent names back the place the person is in, so it is told each place by the window's own
   * English name — the one the activity bar and Settings' domain list show — never a name of its own.
   */
  it("names every view and every domain as the window's English does", () => {
    // Knowledge is named with its mode, below; the twin's Search and Convergence are its two modes (UX6i).
    for (const view of VIEWS.map((each) => each.view).filter((name) => name !== 'settings' && name !== 'knowledge')) {
      expect(prefaceOf({ view, workspace: null }), view)
        .toBe(`Where the person is now: the ${en[`nav.${view}`]} view, every workspace.`);
    }
    // Agents left Settings for a place of its own (UX6e), which the views above name since the twins moved it (UX6e2);
    // Workspace and Permissions left for a workspace's page (UX6g), and Plugins for its place (UX6j), and since UX6g2b the
    // twins name only the domains Settings shows.
    for (const domain of PLACE_DOMAINS) {
      expect(prefaceOf({ view: 'settings', workspace: null, settings: domain }), domain)
        .toBe(`Where the person is now: Settings → ${en[`settings.domain.${domain}`]}, every workspace.`);
    }
  });

  /** UX6i (D150 §2.2): Knowledge is one place in two modes, and the helper is told which, by the names its choice shows. */
  it("names Knowledge's mode, by the names its list's choice shows", () => {
    expect(prefaceOf({ view: 'knowledge', workspace: null, knowledge: 'convergence' }))
      .toBe(`Where the person is now: the ${en['nav.knowledge']} view, showing ${en['nav.convergence']}, every workspace.`);
    expect(prefaceOf({ view: 'knowledge', workspace: 'aurora', knowledge: 'search' }))
      .toBe('Where the person is now: the Knowledge view, showing Search, workspace `aurora`.');
    // Each mode a go may name is one the preface names: since UX6i2a a mode is a part within Knowledge, not a view, and the
    // two are held here so the loop can never run over nothing again.
    const modes = PLACE_PARTS.filter(({ within }) => within === 'knowledge').map(({ part }) => part as KnowledgeMode);
    expect(modes).toEqual(['search', 'convergence']);
    for (const mode of modes) {
      expect(prefaceOf({ view: 'knowledge', workspace: null, knowledge: mode })).toContain(`showing ${en[`nav.${mode}`]}`);
    }
  });

  it('names the session attended on Sessions, and what a parked one asks, on one line and bounded', () => {
    const said = prefaceOf({
      view: 'sessions', workspace: 'aurora',
      session: { id: 'p4rk3d00', repository: 'engine', state: 'awaiting-person', note: `Two ways forward;\n\nI recommend ${'the second '.repeat(60)}` },
    });

    expect(said.startsWith(
      'Where the person is now: the Sessions view, workspace `aurora`, attending session `p4rk3d00` in `engine`, which is waiting on the person. It says: "Two ways forward; I recommend the second',
    )).toBe(true);
    expect(said).not.toContain('\n');
    expect(said.length).toBeLessThan(500);
    expect(said.endsWith('…".')).toBe(true);
  });

  it('says nothing of a session the person is not looking at', () => {
    expect(prefaceOf({ view: 'map', workspace: null, session: { id: 's1', repository: 'engine', state: 'working' } }))
      .toBe('Where the person is now: the Map view, every workspace.');
  });

  it('finds the attended session in the list, and names none it cannot find', () => {
    const sessions = [{ id: 's1', repository: 'engine', state: 'awaiting-person', note: 'Which one?' } as Session];
    expect(attendedOf('s1', sessions))
      .toEqual({ id: 's1', repository: 'engine', state: 'awaiting-person', note: 'Which one?', answered: false });
    expect(attendedOf('gone', sessions)).toBeNull();
    expect(attendedOf(null, sessions)).toBeNull();
  });

  /**
   * ANSWER1f (D131's ANSWER1c note): a park the person answered stays `awaiting-person` until the driver's next look
   * (ANSWER1b), and the same session goes on with the answer then. The helper is told so, as the session list says it,
   * never that it waits on the person. A blank answer is none, as `answeredPark` reads it.
   */
  it('names an answered park as going on with the answer at the next look, never as waiting on the person', () => {
    const sessions = [
      { id: 'p4rk3d00', repository: 'engine', state: 'awaiting-person', note: 'Which port?\n\nAnswered: 8080', answer: '8080' },
      { id: 'b1ank000', repository: 'engine', state: 'awaiting-person', note: 'Which port?', answer: '' },
    ] as Session[];

    const answered = attendedOf('p4rk3d00', sessions);
    expect(answered?.answered).toBe(true);
    const said = prefaceOf({ view: 'sessions', workspace: null, session: answered });
    expect(said).toBe('Where the person is now: the Sessions view, every workspace, attending session `p4rk3d00` in `engine`, '
      + 'which is answered: the same session goes on with the person\'s answer at the driver\'s next look. '
      + 'It says: "Which port? Answered: 8080".');
    expect(said).not.toContain('waiting on the person');

    expect(prefaceOf({ view: 'sessions', workspace: null, session: attendedOf('b1ank000', sessions) }))
      .toContain('which is waiting on the person.');
  });

  /**
   * HELP2: the helper cannot see the window, and asked what the panel held it guessed (DOCK1d's look).
   * On Sessions it is told where the views stand and which region is showing.
   */
  it('says which views each region holds, and whether it is showing', () => {
    expect(prefaceOf({
      view: 'sessions', workspace: null,
      layout: { right: ['timeline', 'ask'], panel: ['console', 'review'], rightShown: true, panelShown: false },
    })).toBe('Where the person is now: the Sessions view, every workspace. The right side bar holds the timeline and Ask Daoris, '
      + 'and is open; the panel holds the console and the review, and is hidden.');
    expect(prefaceOf({
      view: 'sessions', workspace: null,
      layout: { right: ['timeline', 'review', 'ask', 'console'], panel: [], rightShown: false, panelShown: true },
    })).toBe('Where the person is now: the Sessions view, every workspace. The right side bar holds the timeline, the review, '
      + 'Ask Daoris and the console, and is closed; the panel holds nothing.');
    // On every view, since the frame is (DOCK1a); only a caller with no frame leaves it out.
    expect(prefaceOf({ view: 'quests', workspace: null, layout: { right: ['ask'], panel: ['console'], rightShown: true, panelShown: true } }))
      .toBe('Where the person is now: the Quests view, every workspace. The right side bar holds Ask Daoris, and is open; '
        + 'the panel holds the console, and is showing.');
    expect(prefaceOf({ view: 'quests', workspace: null })).toBe('Where the person is now: the Quests view, every workspace.');
    // The person's own terminal (CONSOLE4b), by the name its tab carries.
    expect(prefaceOf({ view: 'quests', workspace: null, layout: { right: ['ask'], panel: ['console', 'terminal'], rightShown: true, panelShown: true } }))
      .toBe('Where the person is now: the Quests view, every workspace. The right side bar holds Ask Daoris, and is open; '
        + 'the panel holds the console and the terminal, and is showing.');
  });

  /** FRAME1i-a (D118): the item a view has chosen, by the id a go names, only while the screen shows it. */
  it('names the item a view has chosen, one case per kind, and says nothing with none', () => {
    const at = (view: Parameters<typeof prefaceOf>[0]['view'], kind: NonNullable<HelpWhere['item']>['kind'], id: string) =>
      prefaceOf({ view, workspace: null, item: { kind, id } });
    expect(at('quests', 'quest', 'abc123')).toBe('Where the person is now: the Quests view, every workspace, looking at quest `abc123`.');
    expect(at('quests', 'ask', 'ask:7c1e9a04b2d5')).toBe('Where the person is now: the Quests view, every workspace, looking at ask `ask:7c1e9a04b2d5`.');
    expect(at('projects', 'repository', 'engine')).toContain(', looking at repository `engine`.');
    expect(at('projects', 'workspace', 'aurora')).toContain(', looking at workspace `aurora`.');
    expect(at('agents', 'agent', 'claude')).toContain(', looking at agent `claude`.');
    expect(at('plugins', 'plugin', 'acme.gate')).toContain(', looking at plugin `acme.gate`.');
    expect(prefaceOf({ view: 'quests', workspace: null, item: { kind: 'quest', id: 'abc123', closed: true } }))
      .toBe('Where the person is now: the Quests view, every workspace, looking at quest `abc123`, which is closed.');
    expect(prefaceOf({ view: 'quests', workspace: null, item: null })).toBe('Where the person is now: the Quests view, every workspace.');
    expect(prefaceOf({ view: 'quests', workspace: null, item: { kind: 'quest', id: 'abc123' }, layout: { right: ['ask'], panel: [], rightShown: true, panelShown: true } }))
      .toContain('looking at quest `abc123`. The right side bar');
  });

  it('reads a view\'s choice and standing: live is said, closed is said as closed, anything else is unsaid', () => {
    expect(itemOf('quests', 'abc123', 'live')).toEqual({ kind: 'quest', id: 'abc123' });
    expect(itemOf('quests', 'abc123', 'closed')).toEqual({ kind: 'quest', id: 'abc123', closed: true });
    expect(itemOf('quests', 'ask:7c1e', 'live')).toEqual({ kind: 'ask', id: 'ask:7c1e' });
    expect(itemOf('projects', 'engine', 'live')).toEqual({ kind: 'repository', id: 'engine' });
    expect(itemOf('projects', 'workspace:aurora', 'live')).toEqual({ kind: 'workspace', id: 'aurora' });
    expect(itemOf('agents', 'claude', 'live')).toEqual({ kind: 'agent', id: 'claude' });
    expect(itemOf('plugins', 'acme.gate', 'live')).toEqual({ kind: 'plugin', id: 'acme.gate' });
    for (const standing of ['gone', 'unread', undefined] as const) expect(itemOf('quests', 'abc123', standing)).toBeNull();
    expect(itemOf('quests', null, 'live')).toBeNull();
    // An offer is not an installed plugin (left to PLUGUI1h); Knowledge and Settings keep what they say today.
    expect(itemOf('plugins', 'offer:acme.gate', 'live')).toBeNull();
    expect(itemOf('knowledge', 'x', 'live')).toBeNull();
    expect(itemOf('settings', 'x', 'live')).toBeNull();
  });

  it('names a working session by its state, and one that asked nothing by no quote', () => {
    expect(prefaceOf({ view: 'sessions', workspace: null, session: { id: 's1', repository: 'engine', state: 'working' } }))
      .toBe('Where the person is now: the Sessions view, every workspace, attending session `s1` in `engine`, which is working.');
  });
});
