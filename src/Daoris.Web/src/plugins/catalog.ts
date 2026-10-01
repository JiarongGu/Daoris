import type { PluginSourceShown } from '../settings/PluginUpdate';

// The Plugins view's list and page as values (PLUGUI1b, D119 §3.1): a plugin's state, its group, and what the list's
// chosen item names. Pure, so every machine a person could have is an argument, and the list, the strip and the page
// read one answer.

/**
 * One installed plugin as the driver's `PLUGINS` answers it (D64). The bridge's own type is the same shape; the
 * view's molecules name their props here rather than reach the bridge.
 */
export type PluginShown = {
  id: string;
  name: string;
  version: string;
  description: string;
  /** The person's word (`plugins.json`), independent of whether the plugin is sound. */
  enabled: boolean;
  /** Why it contributes nothing, in the driver's own sentence; null or absent when sound. */
  problem?: string | null;
  /** The agents it declares, by name. */
  harnesses: string[];
  /** The points it listens on, when it speaks. */
  points: string[];
  /** Whether its hook process is up right now. */
  running: boolean;
  /** Its install folder. */
  folder: string;
  /** Where what it keeps lives, which an update never touches and a removal leaves. */
  data: string;
  /** Where it came from (D103). An older shell sends none. */
  source?: PluginSourceShown;
};

/** One of Daoris's own plugins the install carries (D103), with what its manifest declares and its README needs. */
export type OfferShown = {
  id: string;
  name: string;
  version: string;
  description: string;
  /** Why it cannot be installed as it stands, in the driver's own sentence. */
  problem?: string | null;
  harnesses: string[];
  points: string[];
  servers: string[];
  needs: string[];
  installed: boolean;
};

/**
 * A plugin's state on today's answers. D119 §2 names five; `ready` and `failing` need the loop's own record
 * (`PluginHealth`, PLUGUI1d) and arrive with PLUGUI1f, so until then a sound plugin whose process is not up is
 * simply `on`, and says no word.
 */
export type PluginState = 'running' | 'on' | 'refused' | 'off';

export function pluginState(plugin: Pick<PluginShown, 'enabled' | 'problem' | 'running'>): PluginState {
  // Off is the person's word, and outranks the rest: a refused one that is off is off, its sentence on its page.
  if (!plugin.enabled) return 'off';
  if (plugin.problem) return 'refused';
  return plugin.running ? 'running' : 'on';
}

/** Whether a plugin speaks at a point the driver asks, and so has something to try. A refused one is taken nowhere. */
export const speaks = (plugin: Pick<PluginShown, 'problem' | 'points'>): boolean => !plugin.problem && plugin.points.length > 0;

/** The list's groups (D119 §3.1), each in the person's reading order. */
export type PluginGroups = {
  /** Waiting on you: refused while on. Failing joins it with PLUGUI1f. */
  waiting: PluginShown[];
  /** On: running, or simply on. */
  on: PluginShown[];
  off: PluginShown[];
  /** Daoris's own plugins this machine has not installed. */
  offers: OfferShown[];
};

/**
 * The installed plugins by what they need from the person, then the offers not installed. **The order is what the
 * person acts on** (D119 §3.1): what needs them, what is working, what they turned off, what they could add.
 *
 * Within a group, by name as the person reads it (case and accents aside, in their language), then by id, since two
 * plugins may wear one name.
 */
export function pluginGroups(plugins: readonly PluginShown[], offers: readonly OfferShown[], locale?: string): PluginGroups {
  const collator = new Intl.Collator(locale, { sensitivity: 'base' });
  const byName = <T extends { name: string; id: string }>(rows: T[]) =>
    rows.sort((a, b) => collator.compare(a.name, b.name) || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  const inState = (...states: PluginState[]) => byName(plugins.filter((plugin) => states.includes(pluginState(plugin))));

  return {
    waiting: inState('refused'),
    on: inState('running', 'on'),
    off: inState('off'),
    offers: byName(offers.filter((offer) => !offer.installed)),
  };
}

/** Every installed plugin in the list's order, as the strip marks them: offers are not on the strip. */
export const stripOrder = (groups: PluginGroups): PluginShown[] => [...groups.waiting, ...groups.on, ...groups.off];

const OFFER = 'offer:';

/** An offer's item: an offer and an installed plugin may share an id, so the list names which (D119 §3.1). */
export const offerItem = (id: string) => `${OFFER}${id}`;

/** What the list's chosen item shows in the main area. */
export type Chosen =
  | { kind: 'none' }
  | { kind: 'plugin'; plugin: PluginShown }
  | { kind: 'offer'; offer: OfferShown }
  /** No longer on this machine: removed at a terminal or by hand (D119 §3.2). */
  | { kind: 'gone' };

/**
 * What a chosen item names, on the catalogue as it stands. **Once an offer is installed, the list chooses the
 * installed plugin** (D119 §3.1), so an offer's item that names one installed since reads as that plugin.
 */
export function chosenOf(item: string | null, plugins: readonly PluginShown[], offers: readonly OfferShown[]): Chosen {
  if (!item) return { kind: 'none' };
  const id = item.startsWith(OFFER) ? item.slice(OFFER.length) : null;
  if (id !== null) {
    const offered = offers.find((offer) => offer.id === id && !offer.installed);
    if (offered) return { kind: 'offer', offer: offered };
  }
  const installed = plugins.find((plugin) => plugin.id === (id ?? item));
  return installed ? { kind: 'plugin', plugin: installed } : { kind: 'gone' };
}
