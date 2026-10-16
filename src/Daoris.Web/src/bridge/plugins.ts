import { useMutation, useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../data/queries';
import type { KitPoint, NewPlugin, PluginTrialResult } from '../plugins/PluginKit';
import type { HelpServerShown, PluginOfferShown, SkillShown } from '../plugins/catalog';
import type { PluginSourceShown, PluginUpdatePlanShown } from '../plugins/PluginUpdate';
import { call } from './call';

// This machine's plugins (MOD3): the catalogue, the switches, an update, the install's own, and the kit (D64).

/**
 * This machine's plugins (D64): folders under the home's `plugins/`, each with what it declares,
 * what it speaks on, whether its process is up, and why it contributes nothing when it does not.
 *
 * @remarks
 * Shell-only for the usual reason — a plugin's folder is a machine path (D47 §4) — and read off
 * the same catalogue the driver reads each tick, so what the page shows is what the loop has.
 */
export type PluginEntry = {
  id: string;
  name: string;
  version: string;
  description: string;
  /** The person's word (`plugins.json`), independent of whether the plugin is sound. */
  enabled: boolean;
  /** Why it contributes nothing, in the driver's own sentence; null when sound. */
  problem: string | null;
  /** The harnesses it declares — configurations of the ACP door. */
  harnesses: string[];
  /** The points it listens on, when it speaks. */
  points: string[];
  /** Whether its hook process is up right now. */
  running: boolean;
  folder: string;
  data: string;
  /** Where it came from (PLUG9 c): what an Update re-reads. An older shell sends none. */
  source?: PluginSourceShown;
  /** Whether an update waits (PLUGUI1e): `waits`, `current`, or null with no record that reads. */
  update?: 'waits' | 'current' | null;
  /**
   * Its declared icon as its own bytes, a `data:image/svg+xml` or `data:image/png` URI (PLUGUI2, D140 §3.2), never a
   * path. Null, or absent from a host older than PLUGUI2b, and the page draws the monogram.
   */
  icon?: string | null;
  /** Why its declared icon is not drawn, in the reader's sentence (D140 §3.1). */
  iconProblem?: string | null;
  /** The servers it hands Ask Daoris, with what Ask Daoris may call on each (HELPPLUG1a, D170 point 3); an older shell sends none. */
  helpServers?: HelpServerShown[];
  /** The skills it hands sessions or Ask Daoris, who each goes to, and why one is not handed (HELPPLUG1b); an older shell sends none. */
  skills?: SkillShown[];
};
export type PluginCatalog = {
  folder: string;
  plugins: PluginEntry[];
  /** Where a new plugin may speak (PLUG8): the kit's points, which are the driver's. An older shell sends none. */
  kit?: { points: KitPoint[] };
  /** Daoris's own plugins the install carries (PLUG9 d), each marked installed or not. An older shell sends none. */
  offers?: PluginOfferShown[];
  offersFolder?: string;
};

/**
 * The catalogue. `enabled: false` asks nothing (PLUGUI1b): the application holds the Plugins view on every view, and the
 * view asks only while it is in front; Settings → Driver asks whenever it is drawn, for the plugins folder its row names
 * (UX6j: Settings → Plugins retired into the view).
 */
export const usePlugins = ({ enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.plugins,
    queryFn: () => call<PluginCatalog>('PLUGINS'),
    enabled: isAvailable && enabled,
  });
};

/**
 * The screen's half of `daoris plugin enable|disable|remove` (D50): a row, or the folder gone with the data named. The
 * catalogue, and the roster, since a declared harness came or went with it, are asked again by the route's `STATE_CHANGED`
 * in every window (STATEHUB1d, D169), as for the update and the install below.
 *
 * HELPPLUG1a's tightening (D170's note): `help-approve`, `help-reads` and `help-withdraw`, with the `server`, are the person's
 * approval of the exact tools a plugin's server offers Ask Daoris, its reads only, or its withdrawal, the page's half of
 * `daoris-driver plugins approve|withdraw`.
 */
export const usePluginAction = () => useMutation({
  mutationFn: (action: { id: string; action: 'enable' | 'disable' | 'remove' | 'help-approve' | 'help-reads' | 'help-withdraw'; server?: string }) =>
    call<{ id: string; action: string; data: string | null }>('PLUGIN_ACTION', action),
});

/**
 * The screen's half of `daoris plugin update <id>` (PLUG9 c, D50): without `apply`, what an update would
 * change or why it cannot, for the row to show before the press; with it, the update, and the catalogue and
 * the roster asked again, since what the plugin declares may have changed. One route answers both, so the plan's
 * press asks them again too.
 */
export const usePluginUpdate = () => useMutation({
  mutationFn: (target: { id: string; apply?: boolean }) => call<PluginUpdatePlanShown>('PLUGIN_UPDATE', target),
});

/**
 * The screen's Install beside one of the install's own plugins (PLUG9 d): `daoris plugin add --offer <id>`'s
 * copy. Nothing runs at the press; the catalogue and the roster are asked again by the state hub.
 */
export const usePluginInstall = () => useMutation({
  mutationFn: (offer: string) => call<{ id: string; name: string; version: string }>('PLUGIN_INSTALL', { offer }),
});

/**
 * The screen's half of `daoris-driver plugins new` (PLUG8, D101): a plugin's folder written into the one
 * the person named. Nothing is installed, so nothing is asked again.
 */
export const usePluginNew = () =>
  useMutation({
    mutationFn: (plugin: NewPlugin) =>
      call<{ id: string; folder: string; points: string[]; files: string[] }>('PLUGIN_NEW', plugin),
  });

/**
 * The screen's half of `daoris-driver plugins try`: an installed plugin by its id, or a folder by its
 * path, started as the driver would. It may take as long as the driver waits — two minutes for a landing.
 */
export const usePluginTry = () =>
  useMutation({
    mutationFn: (target: { id: string } | { folder: string }) => call<PluginTrialResult>('PLUGIN_TRY', target),
  });
