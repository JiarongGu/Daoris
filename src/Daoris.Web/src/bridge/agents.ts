import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { keys } from '../queries';
import type { AccountPlace, AccountSettings, AccountSettingsChange, ToolDoor } from '../tools';
import type { WiringAnswer } from '../map/wiring';
import { call } from './call';

// The toolchain (MOD3): this machine's agents and their accounts, what each carried, each tool's own
// actions, and what a start in each workspace would run on (D49 §4, D57).

/**
 * This machine's harnesses, and the accounts they run as (D49 §4, D50).
 *
 * @remarks
 * **Shell-only, like every machine-local surface** — a browser over a keyed remote has no business
 * knowing which tools are installed on somebody's laptop, let alone which accounts they hold. The
 * profile HOME is a filesystem path, which is the sharpest reason this rides the bridge and has no
 * HTTP route (D47 §4).
 *
 * **A sign-in stays the tool's.** `login` runs the harness's own flow into a profile directory and
 * streams it through the console; nothing here reads, stores or forwards a sign-in, and `login` is
 * only ever the person pressing something. The one secret that crosses this bridge is an API key a
 * person types (`key-add`, D67 §1), once, inward — answered only by its last four characters.
 */
export type HarnessRoster = {
  settingsPath: string;
  /** Which adapter this machine spawns sessions with — `driver.json`'s, shown beside the roster. */
  adapter: string;
  /**
   * One row per door, in `tools.ts`'s one type for it (REV3 CLEAN1: this payload had two types, and
   * five readers cast one to the other).
   */
  harnesses: ToolDoor[];
};

/**
 * What sessions on this machine consumed (TOOL3/D57 §4) — measured before it is managed.
 *
 * @remarks
 * 🔴 **Machine-local, by an inherited rule.** Per-account usage names a credential profile, and a
 * profile name is already served only over loopback — so this rides the bridge like the console and
 * the diff, and has no HTTP route at all.
 *
 * **The source is the protocol door.** ACP reports context pressure per turn; the pipe door gives
 * text. So a pipe-door session appears here not at all, and a surface says *not measured* rather
 * than showing a zero.
 *
 * **No price is claimed.** Daoris does not know what a token costs (D24); these are counts somebody
 * else's tool volunteered.
 */
export type UsedSession = {
  session: string;
  repository: string;
  harness: string;
  /** The account it ran as, or null for the harness's own configuration home. */
  profile: string | null;
  /** Context held at its high-water mark, and the window it was held against. */
  used: number;
  size: number;
  when: string;
};
export type UsedAccount = {
  harness: string;
  profile: string | null;
  sessions: number;
  used: number;
};

export const useUsage = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.usage,
    queryFn: () => call<{ sessions: UsedSession[]; accounts: UsedAccount[] }>('USAGE'),
    enabled: isAvailable,
  });
};

export const useHarnesses = () => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.harnesses,
    queryFn: () => call<HarnessRoster>('HARNESSES'),
    enabled: isAvailable,
    // Detection spawns a process per harness, so this is not something to re-run on every focus. The
    // roster has a refresh, and every action refreshes it — which is when it can actually have changed.
    staleTime: Infinity,
  });
};

/**
 * What sessions run as where a default was just set or cleared (LOOK2c): the workspace, or null for the machine; the
 * account, or null for the agent's own configuration home; and which rung answered — the workspace's own account, the
 * machine's default (a workspace naming none falls back to it), or the agent's own home. The same fact `daoris agent
 * profile default … [--clear]` prints.
 */
export type DefaultStanding = {
  workspace?: string | null;
  account?: string | null;
  from: 'workspace' | 'machine' | 'own';
};

/**
 * Install, update, or log a profile in — each by that harness's OWN mechanism.
 *
 * @remarks
 * Never automatic and never mid-session (D49 §4): a tool that changed under a running loop is a
 * moving target nobody diffed. The output arrives as `SESSION_OUTPUT` under `<harness>:<action>`, so
 * the same console component renders it.
 */
export const useHarnessAction = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (action: {
      harness: string;
      action: 'install' | 'update' | 'login' | 'login-new' | 'key-add' | 'pin' | 'unpin'
      | 'profile-add' | 'profile-remove' | 'profile-default' | 'profile-rename' | 'profile-join';
      profile?: string;
      /** An account's name (ACCT2) — `profile-rename` only; its own id, or none, clears it. */
      name?: string;
      /**
       * The lists an account joins (ACCT1) — `profile-join`, and a sign-in's: each a workspace's name, or null for this
       * machine's list.
       */
      join?: (string | null)[];
      /**
       * An API key — `key-add` only (AGT3, D67 §1). It crosses the bridge once, inward; the answer
       * and the roster name it only by its last four characters.
       */
      key?: string;
      /** Which version to install and pin to — `pin` only (TOOL2/D57). */
      version?: string;
      /** Which circle a default is for — `profile-default` only (D49 §4); absent means the machine's. */
      workspace?: string;
    }) => call<{
      harness: string; action: string; exitCode?: number; started?: boolean;
      /**
       * For `key-add`: the account made, and the key's handle; for a sign-in, the account it reaches (ACCT1); for
       * `profile-rename` and `profile-join`, the account named.
       */
      profile?: string; key?: string;
      /** For `profile-rename`: its name now, null where it has none (ACCT2). */
      name?: string | null;
      /** For `profile-join`: where it runs now (ACCT1). */
      places?: AccountPlace[];
      /** For `profile-default`: what sessions there run as now (LOOK2c). Absent on a shell older than it. */
      default?: DefaultStanding;
    }>('HARNESS_ACTION', action),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: keys.harnesses });
      // A join, a default and a rename change what the accounts' files say (ACCT1, ACCT2): the lists and the names.
      void client.invalidateQueries({ queryKey: keys.accounts });
    },
  });
};

/** How a process action ended — as news, after the request that started it was answered. */
export type HarnessEnded = {
  harness: string;
  action: string;
  profile: string | null;
  exitCode: number;
  /** The driver's own sentence when the process failed after it had started; null when it simply exited. */
  problem: string | null;
  /** Who signed in, for a sign-in — the tool's own answer (D66 §3); null when it did not say. */
  account?: string | null;
  /** For a sign-in to another account: whether it left one behind — only when it finished. */
  kept?: boolean | null;
  /** For a sign-in: where the account runs now (ACCT1), none where no list or default holds it; absent from an older shell. */
  places?: AccountPlace[] | null;
};

/**
 * A harness action's end, as news (2026-09-23). An install waits on a network and a login on a
 * person in a browser — longer than any request may take on the bridge — so `HARNESS_ACTION`
 * answers `started` and the end arrives here, the same way a conversation's ending does (D49 §3).
 * The roster is asked again when it does.
 */
export const useHarnessEnded = (handler: (ended: HarnessEnded) => void) => {
  const client = useQueryClient();
  useShenoraEvent('DAORIS', 'HARNESS_ENDED', (payload) => {
    const ended = payload as HarnessEnded | undefined;
    if (!ended?.harness || !ended.action) return;
    void client.invalidateQueries({ queryKey: keys.harnesses });
    handler(ended);
  });
};

/**
 * Answer the prompt a running harness action printed — the sign-in code a login asks to have pasted
 * (2026-09-23). The host refuses it, naming the action, when nothing is running under that name, so
 * a code pasted after the login ended never looks delivered.
 */
export const useHarnessInput = () =>
  useMutation({
    mutationFn: (input: { harness: string; action: string; text: string }) =>
      call<{ sent: boolean }>('HARNESS_INPUT', input),
  });

/** Stop a running harness action — a login the person is not going to finish. */
export const useHarnessCancel = () =>
  useMutation({
    mutationFn: (target: { harness: string; action: string }) =>
      call<{ cancelled: boolean }>('HARNESS_CANCEL', target),
  });

/**
 * Ask the tools again rather than answering from before — the person pressing *Read again* (UX6e, D150 §5.3): every agent,
 * or with `agent` that agent's accounts alone, one at a time, and its own sign-in, or with `profile` too that one account
 * alone (ROSTER1). Never asked by a look, a timer or a view opening: only this press reads an account's state.
 */
export const useRefreshHarnesses = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (target?: { agent?: string; profile?: string }) =>
      call<HarnessRoster>('HARNESSES', {
        refresh: true,
        ...(target?.agent ? { agent: target.agent } : {}),
        ...(target?.agent && target.profile ? { profile: target.profile } : {}),
      }),
    onSuccess: (roster) => {
      client.setQueryData(keys.harnesses, roster);
      // Looking again also lets a refused account through (AGT3b), which changes what a start takes.
      void client.invalidateQueries({ queryKey: keys.allStarts });
    },
  });
};

/**
 * An account's own model and effort (AGT6, D98): keys in the tool's own settings file under that
 * account — the screen's half of `daoris agent settings`, over the same file (D50). Only the keys the
 * change names go on the wire: one left out is untouched, and null clears it. The roster is asked again
 * after, since it reads each account's file.
 */
export const useSetAgentSettings = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ harness, profile, ...change }: { harness: string; profile: string } & AccountSettingsChange) =>
      call<AccountSettings & { harness: string; profile: string }>('SET_AGENT_SETTINGS', {
        harness, profile,
        ...('model' in change ? { model: change.model ?? null } : {}),
        ...('effort' in change ? { effort: change.effort ?? null } : {}),
        ...(change.perModel && Object.keys(change.perModel).length > 0 ? { perModel: change.perModel } : {}),
      }),
    onSuccess: () => void client.invalidateQueries({ queryKey: keys.harnesses }),
  });
};

/** What a start in each of these workspaces would take (MAP1b). Desktop-only: it is this machine's. */
export const useStarts = (workspaces: string[]) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.starts(workspaces),
    queryFn: () => call<WiringAnswer>('STARTS', { workspaces }),
    enabled: isAvailable && workspaces.length > 0,
  });
};
