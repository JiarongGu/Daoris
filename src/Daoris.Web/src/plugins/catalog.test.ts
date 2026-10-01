import { describe, expect, it } from 'vitest';
import { chosenOf, offerItem, type OfferShown, pluginGroups, type PluginShown, pluginState, speaks } from './catalog';

// The Plugins view's list and page on today's answers (PLUGUI1b, D119 §3.1): what state a plugin is in, which
// group it sits in and in what order, and what the chosen item names. Pure, so every machine is an argument.

const plugin = (over: Partial<PluginShown> = {}): PluginShown => ({
  id: 'acme.gate', name: 'Acme gate', version: '1.2.0', description: 'Holds quests overnight.',
  enabled: true, problem: null, harnesses: [], points: ['quest/consider'], running: true,
  folder: 'C:/somewhere/data/plugins/acme.gate', data: 'C:/somewhere/data/plugins/.data/acme.gate',
  ...over,
});

const offer = (over: Partial<OfferShown> = {}): OfferShown => ({
  id: 'in-app-browser', name: 'In-app browser', version: '0.1.0', description: 'Daoris\'s browser for every session.',
  problem: null, harnesses: [], points: [], servers: ['browser'], needs: [], installed: false,
  ...over,
});

describe("a plugin's state, on today's answers", () => {
  it('is off when the person switched it off, whatever else is true', () => {
    expect(pluginState(plugin({ enabled: false }))).toBe('off');
    // A refused one that is off is off too; its sentence stays on its page (D119 §2).
    expect(pluginState(plugin({ enabled: false, problem: 'needs plugin API 99' }))).toBe('off');
  });

  it('is refused when on and the driver refused it', () => {
    expect(pluginState(plugin({ problem: 'needs plugin API 99', running: false }))).toBe('refused');
  });

  /** Ready and failing arrive with PLUGUI1f's health, so a sound plugin whose process is not up says no word. */
  it('is running while its process is up, and simply on otherwise', () => {
    expect(pluginState(plugin())).toBe('running');
    expect(pluginState(plugin({ running: false }))).toBe('on');
  });

  it('speaks where it is sound and listens at a point', () => {
    expect(speaks(plugin())).toBe(true);
    expect(speaks(plugin({ points: [] }))).toBe(false);
    expect(speaks(plugin({ problem: 'refused' }))).toBe(false);
  });
});

describe('the groups', () => {
  /** The order is what the person acts on: what needs them, what is working, what they turned off, what they could add. */
  it('are waiting on you, on, off, then the offers not installed', () => {
    const groups = pluginGroups([
      plugin({ id: 'b', name: 'Bravo', enabled: false }),
      plugin({ id: 'r', name: 'Refused', problem: 'no' }),
      plugin({ id: 'a', name: 'Alpha' }),
      plugin({ id: 'q', name: 'Quiet', running: false, points: [] }),
    ], [offer(), offer({ id: 'landing', name: 'Landing', installed: true })]);

    expect(groups.waiting.map(({ id }) => id)).toEqual(['r']);
    expect(groups.on.map(({ id }) => id)).toEqual(['a', 'q']);
    expect(groups.off.map(({ id }) => id)).toEqual(['b']);
    // An installed offer is a plugin of this machine now, never offered twice.
    expect(groups.offers.map(({ id }) => id)).toEqual(['in-app-browser']);
  });

  it('go by name as the person reads it, then by id', () => {
    const groups = pluginGroups([
      plugin({ id: 'z', name: 'gate' }),
      plugin({ id: 'b', name: 'Gate' }),
      plugin({ id: 'a', name: 'apple' }),
    ], []);

    expect(groups.on.map(({ id }) => id)).toEqual(['a', 'b', 'z']);
  });
});

describe('the chosen item', () => {
  const plugins = [plugin(), plugin({ id: 'in-app-browser', name: 'In-app browser' })];

  it('is nothing until something is chosen', () => {
    expect(chosenOf(null, plugins, [offer()])).toEqual({ kind: 'none' });
  });

  it('names an installed plugin by its id, and an offer as one, since the two may share an id', () => {
    expect(chosenOf('acme.gate', plugins, [])).toMatchObject({ kind: 'plugin', plugin: { id: 'acme.gate' } });
    expect(offerItem('in-app-browser')).toBe('offer:in-app-browser');
    expect(chosenOf('offer:in-app-browser', [plugin()], [offer()])).toMatchObject({ kind: 'offer', offer: { id: 'in-app-browser' } });
  });

  /** Once an offer is installed, the list chooses the installed plugin (D119 §3.1). */
  it('reads an offer that is installed now as the plugin it became', () => {
    expect(chosenOf('offer:in-app-browser', plugins, [offer({ installed: true })]))
      .toMatchObject({ kind: 'plugin', plugin: { id: 'in-app-browser' } });
  });

  /** Removed at a terminal or by hand: the page says so rather than going blank (D119 §3.2). */
  it('is gone when what it names is no longer on this machine', () => {
    expect(chosenOf('nobody', plugins, [])).toEqual({ kind: 'gone' });
    expect(chosenOf('offer:nobody', plugins, [])).toEqual({ kind: 'gone' });
  });
});
