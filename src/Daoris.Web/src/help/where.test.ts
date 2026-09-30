import { describe, expect, it } from 'vitest';
import type { Session } from '../api';
import { en } from '../locales';
import { PLACE_DOMAINS, PLACE_VIEWS } from './places';
import { attendedOf, prefaceOf } from './where';

// HELP1b (D89): where the person is, as the helper is told it ahead of their words.

describe('where the person is', () => {
  it('names the view and the workspace in scope', () => {
    expect(prefaceOf({ view: 'overview', workspace: 'aurora' }))
      .toBe('Where the person is now: the Overview view, workspace `aurora`.');
    expect(prefaceOf({ view: 'quests', workspace: null }))
      .toBe('Where the person is now: the Quests view, every workspace.');
  });

  it('names the settings domain on Settings', () => {
    expect(prefaceOf({ view: 'settings', workspace: null, settings: 'workspace' }))
      .toBe('Where the person is now: Settings → Workspace, every workspace.');
  });

  /**
   * NAME1b: the agent names back the place the person is in, so it is told each place by the window's own
   * English name — the one the activity bar and Settings' domain list show — never a name of its own.
   */
  it("names every view and every domain as the window's English does", () => {
    for (const view of PLACE_VIEWS.filter((name) => name !== 'settings')) {
      expect(prefaceOf({ view, workspace: null }), view)
        .toBe(`Where the person is now: the ${en[`nav.${view}`]} view, every workspace.`);
    }
    for (const domain of PLACE_DOMAINS) {
      expect(prefaceOf({ view: 'settings', workspace: null, settings: domain }), domain)
        .toBe(`Where the person is now: Settings → ${en[`settings.domain.${domain}`]}, every workspace.`);
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
    expect(attendedOf('s1', sessions)).toEqual({ id: 's1', repository: 'engine', state: 'awaiting-person', note: 'Which one?' });
    expect(attendedOf('gone', sessions)).toBeNull();
    expect(attendedOf(null, sessions)).toBeNull();
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

  it('names a working session by its state, and one that asked nothing by no quote', () => {
    expect(prefaceOf({ view: 'sessions', workspace: null, session: { id: 's1', repository: 'engine', state: 'working' } }))
      .toBe('Where the person is now: the Sessions view, every workspace, attending session `s1` in `engine`, which is working.');
  });
});
