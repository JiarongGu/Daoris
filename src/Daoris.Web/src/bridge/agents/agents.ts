import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { keys, USAGE_SLICE } from '../../data/queries';
import type { AccountPlace, AccountSettings, AccountSettingsChange, ToolDoor } from '../../agents/tools';
import type { WiringAnswer } from '../../map/wiring';
import { call } from '../call';
import { readSlice } from '../driver';
import { byInstant } from '../sessions/sessions';
import { mergeKeyed, notPushed, readPushed, type SliceSpec, usePushedLive } from '../state/slices';

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

/** What `USAGE` answers: the sessions newest first, and each account's share. */
export type UsageAnswer = { sessions: UsedSession[]; accounts: UsedAccount[] };

const isUsedSession = (value: unknown): value is UsedSession =>
  typeof value === 'object' && value !== null && typeof (value as { session?: unknown }).session === 'string';

const isUsedAccount = (value: unknown): value is UsedAccount =>
  typeof value === 'object' && value !== null && typeof (value as { harness?: unknown }).harness === 'string';

const records = <T>(part: unknown, is: (value: unknown) => value is T): T[] | null =>
  (typeof part === 'object' && part !== null && !Array.isArray(part) ? Object.values(part).filter(is) : null);

/** By UTF-16 unit, as C#'s `StringComparer.Ordinal` compares. */
const ordinal = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);

/**
 * What sessions consumed, as the shell pushes it (PUSH1e, D182): a keyed slice of two partitions, `sessions` (each measured session
 * under its id) and `accounts` (each account's share under its door and account), a session's end a delta of what it moved
 * (`mergeKeyed`), composed into `USAGE`'s answer in the route's order: the sessions newest first by `when` as an instant (a tie by
 * id, where the route keeps the file's order), the accounts by door, then account, the own home's first (`UsageStateSlice`).
 * Nothing judged yet, or a partition missing, composes nothing, which keeps the pull.
 */
export const usageSlice: SliceSpec = {
  name: USAGE_SLICE,
  key: keys.usage,
  read: () => readSlice(USAGE_SLICE),
  compose: (partitions, judged) => {
    if (!judged) return undefined;
    const sessions = records(partitions.get('sessions'), isUsedSession);
    const accounts = records(partitions.get('accounts'), isUsedAccount);
    if (!sessions || !accounts) return undefined;
    return {
      sessions: sessions.sort((a, b) => byInstant(b.when, a.when) || ordinal(a.session, b.session)),
      accounts: accounts.sort((a, b) => ordinal(a.harness, b.harness) || ordinal(a.profile ?? '', b.profile ?? '')),
    } satisfies UsageAnswer;
  },
  merge: mergeKeyed,
};

/** What sessions consumed: pushed where the shell pushes it (PUSH1e), read from memory with no focus refetch; pulled elsewhere. */
export const useUsage = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const pushed = usePushedLive(USAGE_SLICE);
  return useQuery({
    queryKey: keys.usage,
    queryFn: () => readPushed(client, USAGE_SLICE, () => call<UsageAnswer>('USAGE')),
    enabled: isAvailable,
    ...(pushed ? { staleTime: Infinity, refetchOnWindowFocus: false } : {}),
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
 *
 * What it changed at once (a key, a name, a join, a default: the roster and the accounts' files) is published by its route, and
 * the state hub asks the roster's key again in every window (STATEHUB1h, D169); an action that runs on ends as `HARNESS_ENDED`.
 */
export const useHarnessAction = () =>
  useMutation({
    mutationFn: (action: {
      harness: string;
      action: 'install' | 'update' | 'login' | 'login-new' | 'key-add' | 'key-replace' | 'pin' | 'unpin'
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
       * An API key — `key-add`, and `key-replace` into `profile` (KEYREPLACE1b) (AGT3, D67 §1). It crosses the bridge once, inward; the answer
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
  });

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
  /** The organisation and the plan the tool named beside who (ACCOUNTID1); null where it named none, absent from an older shell. */
  org?: string | null;
  plan?: string | null;
  /**
   * The name the driver offers the account (ACCOUNTID1, `AccountNames.Offered`): who signed in, or with the organisation
   * (else the plan) beside it where another account already has the email; null where none tells them apart and the person
   * names it. The page offers it as it is, and holds no copy of the rule. Absent from an older shell.
   */
  offered?: string | null;
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
    // The roster; and the accounts beneath its key where they are pulled: where the shell pushes them (PUSH1e), the end wakes their
    // owner, whose push writes them.
    void client.invalidateQueries({ queryKey: keys.harnesses, predicate: notPushed(client) });
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
 *
 * Its own route, which declares the agents (HARNESSREFRESH1, D169): the roster's read (`HARNESSES`) declares nothing, since a read
 * that published would make every window read again without end. The state hub asks the roster again in every window, with the
 * starts a looked-at account lets through (AGT3b) and the accounts' files, which a press reads windows into (CODEXUSE1,
 * CODEXUSE3), all under its key; so this asks nothing again itself.
 */
export const useRefreshHarnesses = () =>
  useMutation({
    mutationFn: (target?: { agent?: string; profile?: string }) =>
      call<HarnessRoster>('HARNESSES_REFRESH', {
        ...(target?.agent ? { agent: target.agent } : {}),
        ...(target?.agent && target.profile ? { profile: target.profile } : {}),
      }),
  });

/**
 * An account's own model and effort (AGT6, D98): keys in the tool's own settings file under that
 * account — the screen's half of `daoris agent settings`, over the same file (D50). Only the keys the
 * change names go on the wire: one left out is untouched, and null clears it. The roster reads each account's file, so the
 * route publishes it, and the state hub asks it again in every window (STATEHUB1h, D169).
 */
export const useSetAgentSettings = () =>
  useMutation({
    mutationFn: ({ harness, profile, ...change }: { harness: string; profile: string } & AccountSettingsChange) =>
      call<AccountSettings & { harness: string; profile: string }>('SET_AGENT_SETTINGS', {
        harness, profile,
        ...('model' in change ? { model: change.model ?? null } : {}),
        ...('effort' in change ? { effort: change.effort ?? null } : {}),
        ...(change.perModel && Object.keys(change.perModel).length > 0 ? { perModel: change.perModel } : {}),
      }),
  });

/** What a start in each of these workspaces would take (MAP1b). Desktop-only: it is this machine's. */
export const useStarts = (workspaces: string[]) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.starts(workspaces),
    queryFn: () => call<WiringAnswer>('STARTS', { workspaces }),
    enabled: isAvailable && workspaces.length > 0,
  });
};
