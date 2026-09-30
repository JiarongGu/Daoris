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
      'agents/usage', 'permissions/proposals',
    ]);
  });

  it('are every view the activity bar has and every domain Settings shows, and nothing else', () => {
    expect([...PLACE_VIEWS].sort()).toEqual(VIEWS.map(({ view }) => view).sort());
    expect([...PLACE_DOMAINS].sort()).toEqual([...SETTINGS_SECTIONS].sort());
  });

  it('open where the starters\' doors open: a domain at its card or step, a view, a drawer', () => {
    expect(placeDoor({ view: 'quests' })).toEqual({ view: 'quests' });
    expect(placeDoor({ view: 'settings', domain: 'agents' })).toEqual({ view: 'settings', section: 'agents' });
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'lines' })).toEqual({ view: 'settings', section: 'workspace', anchor: 'lines' });
    expect(placeDoor({ view: 'settings', domain: 'start', part: 'helper' })).toEqual({ view: 'settings', section: 'start', anchor: 'step-helper' });
    expect(placeDoor({ view: 'projects', part: 'import' })).toEqual({ view: 'projects', drawer: 'import' });
  });

  it('refuse a place the window does not have, rather than guess at one', () => {
    expect(placeDoor({ view: 'dashboard' })).toBeNull();
    expect(placeDoor({ view: 'settings', domain: 'billing' })).toBeNull();
    expect(placeDoor({ view: 'settings', domain: 'workspace', part: 'colours' })).toBeNull();
    expect(placeDoor({ view: 'quests', part: 'drawer' })).toBeNull();
  });
});
