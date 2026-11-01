import { useMutation, useQuery } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { keys } from '../data/queries';
import type { KitPoint, NewPlugin, PluginTrialResult } from '../plugins/PluginKit';
import type {
  HelpServerShown, PluginOfferShown, PluginShippedShown, ResourceShown, SkillShown, StepShown, ToolShown,
} from '../plugins/catalog';
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
  /** Against the install's own plugin of its id (LANDPIN3): older, or changed from it; null where it needs no word. */
  shipped?: PluginShippedShown | null;
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
  /** The steps it declares and what they create (FABRIC-UNIT1e, §D9); an older shell sends none. */
  steps?: StepShown[];
  resources?: ResourceShown[];
  /** The tools it declares as its manifest says them (ADOSIGNIN1); nothing is found by reading. An older shell sends none. */
  tools?: ToolShown[];
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
 * `daoris-driver plugins approve|withdraw`. Since HELPPLUG1c `help-approve` carries the `writes` the person ticked, each on its
 * own (`--writes`); without them it approves every write, as an older page's did.
 */
export const usePluginAction = () => useMutation({
  mutationFn: (action: {
    id: string; action: 'enable' | 'disable' | 'remove' | 'help-approve' | 'help-reads' | 'help-withdraw'; server?: string; writes?: string[];
  }) =>
    call<{ id: string; action: string; data: string | null }>('PLUGIN_ACTION', action),
});

/**
 * The screen's half of `daoris plugin update <id>` (PLUG9 c, D50): without `apply`, what an update would
 * change or why it cannot, for the row to show before the press; with it, the update, and the catalogue and
 * the roster asked again, since what the plugin declares may have changed. One route answers both, so the plan's
 * press asks them again too.
 */
export const usePluginUpdate = () => useMutation({
  mutationFn: (target: { id: string; apply?: boolean; shipped?: boolean }) => call<PluginUpdatePlanShown>('PLUGIN_UPDATE', target),
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
 * The person's *Check* of a plugin's declared tools (ADOSIGNIN1, D150 point 7): each found, its version against its range, and
 * each readiness check run. Only this press asks the machine; a look at the page reads the manifest alone.
 */
export const usePluginTools = () =>
  useMutation({
    mutationFn: (plugin: { id: string }) => call<{ id: string; tools: ToolShown[] }>('PLUGIN_TOOLS', plugin),
  });

/** What the person's press of a check's fix came to (`PLUGIN_TOOL_FIX`): refused with a `why` code, or run with the tool read again. */
export type PluginToolFixed = {
  id: string;
  tool: string;
  check: number;
  ran: boolean;
  /** The code of a refusal: `fix_not_declared`, `not_found`, `not_started`, `refused`, `unknown_tool`, `unknown_check`, `no_fix`. */
  why?: string | null;
  message: string;
  exitCode?: number | null;
  install?: string | null;
  /** The tool as its checks read it again after the console closed. */
  standing?: ToolShown | null;
};

/**
 * The person's *Set up* or *Sign in* beside an unmet check (ADOSIGNIN1): opens a console they see, running exactly the
 * manifest's `fix` for one of the plugin's own tools, and answers when it closes with the tool read again. Daoris never signs in.
 * It may take as long as the person does at the console.
 */
export const usePluginToolFix = () =>
  useMutation({
    mutationFn: (press: { id: string; tool: string; check: number }) =>
      call<PluginToolFixed>('PLUGIN_TOOL_FIX', press, { timeoutMs: 30 * 60_000 }),
  });

/**
 * The screen's half of `daoris-driver plugins try`: an installed plugin by its id, or a folder by its
 * path, started as the driver would. It may take as long as the driver waits — two minutes for a landing.
 */
export const usePluginTry = () =>
  useMutation({
    mutationFn: (target: { id: string } | { folder: string }) => call<PluginTrialResult>('PLUGIN_TRY', target),
  });
