import { describe, expect, it } from 'vitest';
import frame from '../SettingsView.tsx?raw';
import registry from './domains.ts?raw';
import { SETTINGS_SECTIONS } from '../SettingsView';
import { en, zh } from '../locales';
import { LIST_BOUNDS } from '../work/layout';
import { rememberedDomain, SETTINGS_DOMAINS } from './domains';

/**
 * Settings by domain (MOD4): the frame, the domain list, and one `settings/<Name>Domain.tsx` per domain.
 * Every Settings domain lived in one file, and eight of eighteen branches in three days edited it
 * (`docs/2026-09-30-parallel-development-design.md`). The list in `domains.ts` is the only place a domain
 * is added; these hold that the frame stays a frame and that the list is the whole truth.
 */

const files = import.meta.glob('./*Domain.tsx', { eager: true }) as Record<string, Record<string, unknown>>;

describe('the Settings registry', () => {
  it('is looking at files at all', () => {
    expect(Object.keys(files).length).toBeGreaterThan(5);
  });

  it('registers every domain file, by the component its name names', () => {
    // A file named as a domain is an organism by that name (presentational.test.ts), so an unregistered
    // one would be a hook-holder no list names.
    const components = new Set(SETTINGS_DOMAINS.map((domain) => domain.component as unknown));
    const unregistered = Object.entries(files)
      .map(([path, module]) => [path, module[path.replace(/^\.\/(\w+)\.tsx$/, '$1')]] as const)
      .filter(([, component]) => !components.has(component))
      .map(([path]) => path);
    expect(unregistered).toEqual([]);
  });

  it('names each domain once, each with a label both languages carry', () => {
    const ids = SETTINGS_DOMAINS.map((domain) => domain.id);
    expect(new Set(ids).size).toBe(ids.length);
    for (const { id, label } of SETTINGS_DOMAINS) {
      expect(en[label], `${id}: en ${label}`).toBeTruthy();
      expect(zh[label], `${id}: zh ${label}`).toBeTruthy();
    }
  });

  it('is what the frame offers: its sections are the list, in its order', () => {
    expect(SETTINGS_SECTIONS).toEqual(SETTINGS_DOMAINS.map((domain) => domain.id));
  });

  /**
   * A setting lives on the thing it is about (D150 §1): Agents left for its place (UX6e), Workspace and Permissions for a
   * workspace's page and a repository's (UX6g, §3.1), and Plugins for its place, its folder's row to Driver (UX6j, §2.3).
   * What stays is the machine's or the person's: seven domains.
   */
  it('holds what is the machine\'s or the person\'s: seven domains, no agent, workspace, permissions or plugins domain', () => {
    expect(SETTINGS_DOMAINS.map((domain) => domain.id)).toEqual([
      'start', 'appearance', 'ai', 'driver', 'tools', 'browser', 'logs',
    ]);
    for (const gone of ['Workspace', 'Permissions', 'Agents', 'Plugins']) {
      expect(Object.keys(files)).not.toContain(`./${gone}Domain.tsx`);
      expect(en[`settings.domain.${gone.toLowerCase()}`]).toBeUndefined();
    }
  });

  /**
   * UX6j (D150 §2.3, D116): the first domain is *Get started* again, as D97 named it, since *Setup* names a repository's
   * and a workspace's tab now. Its id was always `start`.
   */
  it('names the first domain Get started, which Setup no longer names', () => {
    expect(SETTINGS_DOMAINS[0]).toMatchObject({ id: 'start', label: 'settings.domain.start' });
    expect([en['settings.domain.start'], zh['settings.domain.start']]).toEqual(['Get started', '开始使用']);
    expect(en['projects.tab.setup']).toBe('Setup');
  });

  /**
   * UX6j: a domain remembered under `daoris.settings` from before it left opens where what it held stayed: Plugins at
   * Driver, which keeps its folder's row and the door to the Plugins place. One that left for a page Settings holds no row
   * of (Agents, Workspace, Permissions) opens on Appearance, as any domain this window lacks does.
   */
  it('opens a remembered domain that left Settings where what it held stayed, else on Appearance', () => {
    expect(rememberedDomain('driver')).toBe('driver');
    expect(rememberedDomain('plugins')).toBe('driver');
    for (const gone of ['agents', 'workspace', 'permissions', 'billing', null]) expect(rememberedDomain(gone)).toBe('appearance');
  });

  /**
   * FRAME1g: the frame is Settings as the window's frame takes it — its list pane, which `DomainList` draws,
   * and its main area, which shows whichever domain the list chose — and Settings drawn alone. It names no
   * domain itself.
   */
  it('is the frame\'s only way to a domain: it imports only the list and its rows from Settings\', and declares only the frame', () => {
    const reached = new Set([...frame.matchAll(/from\s+'\.\/settings\/([\w./-]+)'/g)].map((match) => match[1]));
    expect([...reached].sort()).toEqual(['DomainList', 'domains']);
    const declared = [...frame.matchAll(/^(?:export\s+)?function\s+(\w+)/gm)].map((match) => match[1]);
    expect(declared.sort()).toEqual(['SettingsMain', 'SettingsView', 'useSettingsLayout']);
    for (const { id } of SETTINGS_DOMAINS) expect(frame, id).not.toMatch(new RegExp(`\\b${id[0]!.toUpperCase()}${id.slice(1)}Domain\\b`));
  });

  it('is a list pane with Settings\' own bounds (D118 §3a): 176 to 320 px, from 176', () => {
    expect(LIST_BOUNDS.settings).toEqual({ min: 176, max: 320, initial: 176 });
    expect(frame).toMatch(/view:\s*'settings'/);
  });

  it('imports each domain it registers from the file named for it', () => {
    for (const path of Object.keys(files)) {
      const name = path.replace(/^\.\/(\w+)\.tsx$/, '$1');
      expect(registry, name).toContain(`from './${name}'`);
    }
  });
});
