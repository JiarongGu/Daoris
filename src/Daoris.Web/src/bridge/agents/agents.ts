import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import { keys, USAGE_SLICE } from '../../data/queries';
import type { AccountSettings, AccountSettingsChange, AgentSheet, ToolDoor } from '../../agents/tools';
import type { WiringAnswer } from '../../map/wiring';
import { call } from '../call';
import { readSlice } from '../driver';
import { byInstant } from '../sessions/sessions';
import { mergeKeyed, notPushed, readPushed, type SliceSpec } from '../state/slices';

// The toolchain: this machine's agents and their accounts, what each carried, each tool's own
// actions, and what a start in each workspace would run on.

/**
 * This machine's harnesses, and the accounts they run as.
 *
 * @remarks
 * **Shell-only, like every machine-local surface** — a browser over a keyed remote has no business
 * knowing which tools are installed on somebody's laptop, let alone which accounts they hold. The
 * profile HOME is a filesystem path, which is the sharpest reason this rides the bridge and has no
 * HTTP route.
 *
 * **A sign-in stays the tool's.** `login` runs the harness's own flow into a profile directory and
 * streams it through the console; nothing here reads, stores or forwards a sign-in, and `login` is
 * only ever the person pressing something. The one secret that crosses this bridge is an API key a
 * person types (`key-add`), once, inward — answered only by its last four characters.
 */
export type HarnessRoster = {
  settingsPath: string;
  /** Which adapter this machine spawns sessions with — `driver.json`'s, shown beside the roster. */
  adapter: string;
  /**
   * One row per door, in `tools.ts`'s one type for it.
   */
  harnesses: ToolDoor[];
  /**
   * The sheet's half: each agent once with its account acts and its doors' sheets, which the page reads every agent
   * fact from. Absent from a story's or a test's roster that names none.
   */
  agents?: AgentSheet[];
};

/**
 * What sessions on this machine consume — measured before it is managed.
 *
 * @remarks
 * 🔴 **Machine-local.** Per-account usage names a credential profile, and a
 * profile name is already served only over loopback — so this rides the bridge like the console and
 * the diff, and has no HTTP route at all.
 *
 * **The source is the protocol door.** ACP reports context pressure per turn; the pipe door gives
 * text. So a pipe-door session appears here not at all, and a surface says *not measured* rather
 * than showing a zero.
 *
 * **No price is claimed.** Daoris does not know what a token costs; these are counts somebody
 * else's tool volunteered.
 */
export type UsedSession = {
  session: string;
  repository: string;
  door: string;
  /** The account it ran as, or null for the door's own configuration home. */
  profile: string | null;
  /** Context held at its high-water mark, and the window it is held against. */
  used: number;
  size: number;
  when: string;
};
/** One account's share, counted by agent: one account used through both of an agent's doors is one share. */
export type UsedAccount = {
  /** The agent its sessions' doors are: the provider's id, so `claude-code-acp`'s sessions count under `claude-code`. */
  agent: string;
  profile: string | null;
  sessions: number;
  used: number;
};

/** What `USAGE` answers: the sessions newest first, and each account's share. */
export type UsageAnswer = { sessions: UsedSession[]; accounts: UsedAccount[] };

const isUsedSession = (value: unknown): value is UsedSession =>
  typeof value === 'object' && value !== null && typeof (value as { session?: unknown }).session === 'string';

const isUsedAccount = (value: unknown): value is UsedAccount =>
  typeof value === 'object' && value !== null && typeof (value as { agent?: unknown }).agent === 'string';

const records = <T>(part: unknown, is: (value: unknown) => value is T): T[] | null =>
  (typeof part === 'object' && part !== null && !Array.isArray(part) ? Object.values(part).filter(is) : null);

/** By UTF-16 unit, as C#'s `StringComparer.Ordinal` compares. */
const ordinal = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);

/**
 * What sessions consume, as the shell pushes it: a keyed slice of two partitions, `sessions` (each measured session
 * under its id) and `accounts` (each account's share under its agent and account), a session's end a delta of what it moved
 * (`mergeKeyed`), composed into `USAGE`'s answer in the route's order: the sessions newest first by `when` as an instant (a tie by
 * id, where the route keeps the file's order), the accounts by agent, then account, the own home's first (`UsageStateSlice`).
 * Nothing judged yet, or a partition missing, composes nothing, which is waited for.
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
      accounts: accounts.sort((a, b) => ordinal(a.agent, b.agent) || ordinal(a.profile ?? '', b.profile ?? '')),
    } satisfies UsageAnswer;
  },
  merge: mergeKeyed,
};

/** What sessions consume, as the shell pushes it: read from memory with no focus refetch. */
export const useUsage = () => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  return useQuery({
    queryKey: keys.usage,
    queryFn: () => readPushed<UsageAnswer>(client, USAGE_SLICE),
    enabled: isAvailable,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
};

export const useHarnesses = ({ enabled = true }: { enabled?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.harnesses,
    queryFn: () => call<HarnessRoster>('HARNESSES'),
    enabled: isAvailable && enabled,
    // Detection spawns a process per harness, so this is not something to re-run on every focus. The
    // roster has a refresh, and every action refreshes it — which is when it can actually have changed.
    staleTime: Infinity,
  });
};

/**
 * Install, update, or log a profile in — each by that harness's OWN mechanism.
 *
 * @remarks
 * Never automatic and never mid-session: a tool that changed under a running loop is a
 * moving target nobody diffed. The output arrives as `SESSION_OUTPUT` under `<harness>:<action>`, so
 * the same console component renders it.
 *
 * What it changed at once (a key, a name: the roster and the accounts' files) is published by its route, and the state hub asks
 * the roster's key again in every window; an action that runs on ends as `HARNESS_ENDED`. The bridge refuses `profile-default`, `profile-join` and a sign-in's `join` as
 * `ACCOUNTS_ONE_SET`, since every account serves every workspace.
 */
export const useHarnessAction = () =>
  useMutation({
    mutationFn: (action: {
      door: string;
      action: 'install' | 'update' | 'login' | 'login-new' | 'key-add' | 'key-replace' | 'pin' | 'unpin'
      | 'profile-add' | 'profile-remove' | 'profile-rename';
      profile?: string;
      /** An account's name — `profile-rename` only; its own id, or none, clears it. */
      name?: string;
      /**
       * An API key — `key-add`, and `key-replace` into `profile`. It crosses the bridge once, inward; the answer
       * and the roster name it only by its last four characters.
       */
      key?: string;
      /** Which version to install and pin to — `pin` only. */
      version?: string;
      /**
       * A sign-in's way: `browser`, the agent's own and the first, or `device-code`; absent is the first. The
       * terminal's `--device-code`.
       */
      way?: 'browser' | 'device-code';
      /** Whether this sign-in takes the place of the same one running another way, which the driver stops first. */
      switch?: boolean;
    }) => call('HARNESS_ACTION', action) as Promise<{
      door: string; action: string; exitCode?: number; started?: boolean;
      /**
       * For `key-add`: the account made, and the key's handle; for a sign-in, the account it reaches; for
       * `profile-rename`, the account named.
       */
      profile?: string; key?: string;
      /** For `profile-rename`: its name now, null where it has none. */
      name?: string | null;
    }>,
  });

/** How a process action ended — as news, after the request that started it is answered. */
export type HarnessEnded = {
  door: string;
  action: string;
  profile: string | null;
  exitCode: number;
  /** The driver's own sentence when the process failed after it had started; null when it simply exited. */
  problem: string | null;
  /** Who signed in, for a sign-in — the tool's own answer; null when it does not say. */
  account?: string | null;
  /** The organisation and the plan the tool names beside who; null where it names none. */
  org?: string | null;
  plan?: string | null;
  /**
   * The name the driver offers the account (`AccountNames.Offered`): who signed in, or with the organisation
   * (else the plan) beside it where another account already has the email; null where none tells them apart and the person
   * names it. The page offers it as it is, and holds no copy of the rule.
   */
  offered?: string | null;
  /** For a sign-in to another account: whether it left one behind — only when it finished. */
  kept?: boolean | null;
  /** Whether the person's *Cancel* stopped it, so its end is no failure to say. */
  cancelled?: boolean | null;
  /**
   * Whether it is stopped for the same sign-in by another way, which runs on in the same panel: its end says
   * nothing.
   */
  switched?: boolean | null;
};

/**
 * A harness action's end, as news. An install waits on a network and a login on a
 * person in a browser — longer than any request may take on the bridge — so `HARNESS_ACTION`
 * answers `started` and the end arrives here, the same way a conversation's ending does.
 * The roster is asked again when it does.
 */
export const useHarnessEnded = (handler: (ended: HarnessEnded) => void) => {
  const client = useQueryClient();
  useShenoraEvent('DAORIS', 'HARNESS_ENDED', (payload) => {
    const ended = payload as HarnessEnded | undefined;
    if (!ended?.door || !ended.action) return;
    // The roster; and the accounts beneath its key where they are pulled: where the shell pushes them, the end wakes their
    // owner, whose push writes them.
    void client.invalidateQueries({ queryKey: keys.harnesses, predicate: notPushed(client) });
    handler(ended);
  });
};

/**
 * Answer the prompt a running harness action printed — the sign-in code a login asks to have pasted.
 * The host refuses it, naming the action, when nothing is running under that name, so
 * a code pasted after the login ended never looks delivered.
 */
export const useHarnessInput = () =>
  useMutation({
    mutationFn: (input: { door: string; action: string; text: string }) =>
      call('HARNESS_INPUT', input) as Promise<{ sent: boolean }>,
  });

/** Stop a running harness action — a login the person is not going to finish. */
export const useHarnessCancel = () =>
  useMutation({
    mutationFn: (target: { door: string; action: string }) =>
      call('HARNESS_CANCEL', target) as Promise<{ cancelled: boolean }>,
  });

/**
 * Ask the tools again rather than answering from memory — the person pressing *Read again*: every agent,
 * or with `agent` that agent's accounts alone, one at a time, and its own sign-in, or with `profile` too that one account
 * alone. Never asked by a look, a timer or a view opening: only this press reads an account's state.
 *
 * Its own route, which declares the agents: the roster's read (`HARNESSES`) declares nothing, since a read
 * that published would make every window read again without end. The state hub asks the roster again in every window, with the
 * starts a looked-at account lets through and the accounts' files, which a press reads windows into,
 * all under its key; so this asks nothing again itself.
 */
export const useRefreshHarnesses = () =>
  useMutation({
    mutationFn: (target?: { agent?: string; profile?: string }) =>
      call('HARNESSES_REFRESH', {
        ...(target?.agent ? { agent: target.agent } : {}),
        ...(target?.agent && target.profile ? { profile: target.profile } : {}),
      }) as Promise<HarnessRoster>,
  });

/**
 * An account's own model and effort: keys in the tool's own settings file under that
 * account — the screen's half of `daoris agent settings`, over the same file. Only the keys the
 * change names go on the wire: one left out is untouched, and null clears it. The roster reads each account's file, so the
 * route publishes it, and the state hub asks it again in every window.
 */
export const useSetAgentSettings = () =>
  useMutation({
    mutationFn: ({ door, profile, ...change }: { door: string; profile: string } & AccountSettingsChange) =>
      call('SET_AGENT_SETTINGS', {
        door, profile,
        ...('model' in change ? { model: change.model ?? null } : {}),
        ...('effort' in change ? { effort: change.effort ?? null } : {}),
        ...(change.perModel && Object.keys(change.perModel).length > 0 ? { perModel: change.perModel } : {}),
      }) as Promise<AccountSettings & { door: string; profile: string }>,
  });

/** What a start in each of these workspaces would take. Desktop-only: it is this machine's. */
export const useStarts = (workspaces: string[]) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.starts(workspaces),
    queryFn: () => call('STARTS', { workspaces }) as Promise<WiringAnswer>,
    enabled: isAvailable && workspaces.length > 0,
  });
};
