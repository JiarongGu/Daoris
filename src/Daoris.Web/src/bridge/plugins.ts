import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { KitPoint, NewPlugin, PluginTrialResult } from '../settings/PluginKit';
import type { PluginOfferShown } from '../settings/PluginOffers';
import type { PluginSourceShown, PluginUpdatePlanShown } from '../settings/PluginUpdate';
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

export const usePlugins = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.plugins,
    queryFn: () => call<PluginCatalog>('PLUGINS'),
    enabled: isAvailable,
  });
};

/** The screen's half of `daoris plugin enable|disable|remove` (D50): a row, or the folder gone with the data named. */
export const usePluginAction = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (action: { id: string; action: 'enable' | 'disable' | 'remove' }) =>
      call<{ id: string; action: string; data: string | null }>('PLUGIN_ACTION', action),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.plugins });
      // A declared harness came or went with it, so the roster is asked again too.
      void client.invalidateQueries({ queryKey: keys.harnesses });
    },
  });
};

/**
 * The screen's half of `daoris plugin update <id>` (PLUG9 c, D50): without `apply`, what an update would
 * change or why it cannot, for the row to show before the press; with it, the update, and the catalogue and
 * the roster asked again, since what the plugin declares may have changed.
 */
export const usePluginUpdate = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (target: { id: string; apply?: boolean }) => call<PluginUpdatePlanShown>('PLUGIN_UPDATE', target),
    onSuccess: (plan) => {
      if (!plan.applied) return;
      void client.invalidateQueries({ queryKey: keys.plugins });
      void client.invalidateQueries({ queryKey: keys.harnesses });
    },
  });
};

/**
 * The screen's Install beside one of the install's own plugins (PLUG9 d): `daoris plugin add --offer <id>`'s
 * copy. Nothing runs at the press; the catalogue and the roster are asked again.
 */
export const usePluginInstall = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (offer: string) => call<{ id: string; name: string; version: string }>('PLUGIN_INSTALL', { offer }),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.plugins });
      void client.invalidateQueries({ queryKey: keys.harnesses });
    },
  });
};

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
