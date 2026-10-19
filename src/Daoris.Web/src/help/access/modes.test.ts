import { describe, expect, it } from 'vitest';
import type { HelpReceipt, HelpWiredLack } from '../../bridge/help';
import type { PluginEntry } from '../../bridge/plugins';
import { chipOf, fullAdds, lackDoor } from './modes';

// HELPMODE1b (D183 points 2-4; the modes design §1, §5, §6): the chip's mode, what full access would add, and the door each lack
// names, as values.

const receipt = (over: Partial<HelpReceipt>): HelpReceipt => ({
  at: '2026-10-19T10:00:00+00:00', start: 'new', mode: 'restricted', harness: 'claude-code-acp', version: null, account: null,
  door: 'acp', posture: 'default', permissionMode: null, folder: 'room', tools: [], skills: [], servers: [], rules: null,
  guarded: false, checkouts: [], lacks: [], ...over,
});

const lack = (over: Partial<HelpWiredLack>): HelpWiredLack => ({ kind: 'server', name: 'browser', door: 'plugins', why: 'not-approved', plugin: null, ...over });

const plugin = (over: Partial<PluginEntry>): PluginEntry => ({
  id: 'p', name: 'P', version: '1.0.0', description: '', enabled: true, problem: null, harnesses: [], points: [], running: false,
  folder: 'f', data: 'd', ...over,
});

describe('the chip (design §1: selected, effective and recorded kept apart)', () => {
  it('shows the mode the conversation in view runs under, and the one chosen before any start', () => {
    expect(chipOf('restricted', 'restricted')).toEqual({ now: 'restricted', next: null });
    expect(chipOf('full', 'full')).toEqual({ now: 'full', next: null });
    expect(chipOf('full', null)).toEqual({ now: 'full', next: null });
    expect(chipOf('restricted', null)).toEqual({ now: 'restricted', next: null });
  });

  it('says a switch that waits for the next start, both ways', () => {
    expect(chipOf('full', 'restricted')).toEqual({ now: 'restricted', next: 'full' });
    expect(chipOf('restricted', 'full')).toEqual({ now: 'full', next: 'restricted' });
  });
});

describe('what full access would add', () => {
  it('names each server restricted mode withheld that full access hands: unapproved, changed, refused, for sessions alone', () => {
    const newest = receipt({
      lacks: [
        lack({ name: 'browser', door: 'plugins', why: 'not-approved', plugin: 'browser' }),
        lack({ name: 'tracker', door: 'plugins', why: 'changed', plugin: 'tracker' }),
        lack({ name: 'runner', door: 'mode', why: 'refused' }),
        lack({ name: 'deploy', door: 'mode', why: 'not-for-help', plugin: 'deploy' }),
        // Full access brings no host and no other agent's door, and the shell is no server.
        lack({ name: 'pages', door: 'host', why: 'browser.no-host' }),
        lack({ name: 'files', door: 'harness', why: 'no-door' }),
        lack({ kind: 'shell', name: 'shell', door: 'mode', why: 'restricted' }),
      ],
    });
    expect(fullAdds(newest, undefined)).toEqual(['browser', 'tracker', 'runner', 'deploy']);
  });

  it('adds the servers the plugins offer Ask Daoris that wait for an approval, each once, from a plugin that contributes', () => {
    const plugins = [
      plugin({ id: 'browser', helpServers: [{ name: 'browser', allow: [], state: 'waits' }, { name: 'reader', allow: [], state: 'approved' }] }),
      plugin({ id: 'off', enabled: false, helpServers: [{ name: 'quiet', allow: [], state: 'waits' }] }),
      plugin({ id: 'broken', problem: 'its manifest does not read', helpServers: [{ name: 'broken', allow: [], state: 'waits' }] }),
      plugin({ id: 'runner', helpServers: [{ name: 'runner', allow: [], state: 'refused' }] }),
    ];
    expect(fullAdds(null, plugins)).toEqual(['browser', 'runner']);
    expect(fullAdds(receipt({ lacks: [lack({ name: 'browser', plugin: 'browser' })] }), plugins)).toEqual(['browser', 'runner']);
    // A receipt kept under full access lacks nothing: what the plugins wait on is still what restricted would withhold.
    expect(fullAdds(receipt({ mode: 'full' }), plugins)).toEqual(['browser', 'runner']);
    expect(fullAdds(null, undefined)).toEqual([]);
  });
});

describe('the door each lack names (design §6)', () => {
  it('opens its plugin’s page, the mode picker, the browser’s or this install’s settings, or the agent’s choice', () => {
    expect(lackDoor(lack({ door: 'plugins', plugin: 'browser' }))).toEqual({ view: 'plugins', item: 'browser' });
    expect(lackDoor(lack({ door: 'plugins', plugin: null }))).toEqual({ view: 'plugins' });
    expect(lackDoor(lack({ door: 'mode', why: 'not-for-help' }))).toBe('mode');
    expect(lackDoor(lack({ kind: 'edits', name: 'edits', door: 'mode', why: 'restricted' }))).toBe('mode');
    expect(lackDoor(lack({ door: 'host', why: 'browser.no-host' }))).toEqual({ view: 'settings', section: 'browser' });
    expect(lackDoor(lack({ door: 'host', why: 'browser.failed' }))).toEqual({ view: 'settings', section: 'browser' });
    expect(lackDoor(lack({ door: 'host', why: 'not-found' }))).toEqual({ view: 'settings', section: 'wiring' });
    expect(lackDoor(lack({ door: 'harness', why: 'no-door' }))).toEqual({ view: 'settings', section: 'ai' });
    // A door a newer driver names that this page does not know is said, with no press.
    expect(lackDoor(lack({ door: 'elsewhere' }))).toBeNull();
  });
});
