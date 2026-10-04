import { describe, expect, it } from 'vitest';
import { VIEWS } from '../commands';
import { SETTINGS_SECTIONS } from '../SettingsView';
import { PLACE_DOMAINS, PLACE_PARTS, PLACE_VIEWS, placeDoor } from './places';

// HELP6: the places Ask Daoris's go may name. A twin (`.claude/knowledge/twins.md`) of the driver's
// `HelpPlaces`, which judges a go before the person sees it: the tables below are the driver test's
// `The_places_are_the_pages_twin`, line for line.

describe('the places a go may name', () => {
  it('are the driver\'s table, line for line', () => {
    expect(PLACE_VIEWS).toEqual(['overview', 'sessions', 'quests', 'projects', 'map', 'convergence', 'search', 'settings']);
    expect(PLACE_DOMAINS).toEqual(['start', 'appearance', 'ai', 'workspace', 'driver', 'agents', 'permissions', 'plugins', 'browser', 'logs']);
    expect(PLACE_PARTS.map(({ within, part }) => `${within}/${part}`)).toEqual([
      'projects/add', 'projects/import',
      'start/agent', 'start/helper', 'start/repositories', 'start/driven', 'start/landing', 'start/rules',
      'workspace/wiring', 'workspace/lines', 'workspace/landing', 'workspace/sweep',
      'agents/usage', 'permissions/proposals', 'permissions/across',
    ]);
  });

  /**
   * Plugins is a view since PLUGUI1b, and a go still names Settings → `plugins` until PLUGUI1c moves it to the
   * views in this table and the driver's `HelpPlaces` together, since the twins change together (D119 §5).
   * Settings → Tools (TOOLS7) is a place a go names once TOOLS8 adds it here and to `HelpPlaces` together (D121 §4.3).
   * Agents is a place since UX6e, and a go still names Settings → `agents` until the twins move it together: the door
   * opens the place (D150 §3.1).
   */
  it('are every view the activity bar has and every domain Settings shows, and nothing else', () => {
    expect([...PLACE_VIEWS].sort()).toEqual(VIEWS.map(({ view }) => view).filter((view) => view !== 'plugins' && view !== 'agents').sort());
    expect([...PLACE_DOMAINS].filter((domain) => domain !== 'agents').sort()).toEqual([...SETTINGS_SECTIONS].filter((domain) => domain !== 'tools').sort());
  });

  it('open where the starters\' doors open: a domain at its card or step, a view, a drawer', () => {
    expect(placeDoor({ view: 'quests' })).toEqual({ view: 'quests' });
    // UX6e: Settings → Agents, its Usage and Permissions' Proposals moved to the Agents place, and a go opens them there.
    expect(placeDoor({ view: 'settings', domain: 'agents' })).toEqual({ view: 'agents' });
    expect(placeDoor({ view: 'settings', domain: 'agents', part: 'usage' })).toEqual({ view: 'agents', agentPart: 'usage' });
    expect(placeDoor({ view: 'settings', domain: 'permissions', part: 'proposals' })).toEqual({ view: 'agents', agentPart: 'rules' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'lines' })).toEqual({ view: 'settings', section: 'workspace', anchor: 'lines' });
    expect(placeDoor({ view: 'settings', domain: 'start', part: 'helper' })).toEqual({ view: 'settings', section: 'start', anchor: 'step-helper' });
    expect(placeDoor({ view: 'projects', part: 'import' })).toEqual({ view: 'projects', drawer: 'import' });
    // HELP10: the card READ1 built, found by its own `settings-across`.
    expect(placeDoor({ view: 'settings', domain: 'permissions', part: 'across' }))
      .toEqual({ view: 'settings', section: 'permissions', anchor: 'across' });
  });

  it('refuse a place the window does not have, rather than guess at one', () => {
    expect(placeDoor({ view: 'dashboard' })).toBeNull();
    expect(placeDoor({ view: 'settings', domain: 'billing' })).toBeNull();
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'colours' })).toBeNull();
    expect(placeDoor({ view: 'quests', part: 'drawer' })).toBeNull();
  });
});
