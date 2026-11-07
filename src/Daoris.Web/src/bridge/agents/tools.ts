import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import type {
  GitSwitchShown, ToolEndShown, ToolLookShown, ToolsShown, ToolWay,
} from '../../settings/Tools';
import { call, toolLookBound } from '../call';
import { askHeld, isPending } from '../state/held';
import { ownWindowName } from '../../work/window';

// The programs Daoris runs beside its agents: how each is run, a version downloaded and followed, a
// delete, the resource locations, and what a switch of git changes. The same `tools.json` the terminal's
// `daoris tool` edits.

/**
 * The tools' own query keys, under a root of their own rather than the driver's: the driver's state is asked again on
 * every tick, and the list with versions starts each program. Kept here, as Ask Daoris's proposals keep theirs, so
 * this domain's change touches no shared file. The root is what the state hub asks again when a tools press publishes:
 * the list, and what a switch of git would change.
 */
export const ALL_TOOLS = ['tools'] as const;
const toolsList = (ask: boolean) => ['tools', 'list', ask] as const;
const gitSwitch = (target: GitTarget) => ['tools', 'git', target.way, target.version ?? '', target.file ?? ''] as const;

/** A git a switch would run (§4.1): the system's, a managed version, or a named file. */
export type GitTarget = { way: ToolWay; version?: string; file?: string };

/** A download or a use, as the shell answers its start: started and followed, or ended in the answer itself. */
export type ToolStart = {
  tool: string;
  action: string;
  started: boolean;
  version?: string | null;
  ended?: ToolEndShown;
  /** For a way set at once: the way, and a named file's answer to its version. */
  way?: ToolWay;
  file?: string;
};

/**
 * Every tool as it is run, what is downloaded and offered, and the locations. Shell-only: a tool's file is a
 * machine path. Without `ask` it reads files and answers at once; with it, the shell asks each tool's file its
 * version, which starts the program, so the screen draws from the first and fills in from the second.
 */
export const useTools = ({ ask = false }: { ask?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = toolsList(ask);
  return useQuery({
    queryKey,
    // Told by event: the shell answers what it holds, and its read of the files comes as `ANSWERED`.
    // Nothing held yet reads as null, which the screen draws as loading until the event comes.
    queryFn: () => askHeld('TOOLS_LIST', client, queryKey, ask ? { ask: true } : {},
      (answer): ToolsShown | null => (isPending(answer) ? null : answer as ToolsShown)),
    enabled: isAvailable,
  });
};

/**
 * The screen's half of `daoris tool use <tool> system|managed|file`: the system's and a named file are written at
 * once, a named file only once it answers a version; managed downloads a version that is not here, then switches, and
 * is followed as a download is.
 *
 * Every press here that writes what the list reads is published by its route, and the state hub asks the tools again in
 * every window: the hooks ask nothing again themselves.
 */
export const useToolUse = () =>
  useMutation({
    mutationFn: (use: { tool: string; action: 'system' | 'managed' | 'file'; version?: string; file?: string }) =>
      call('TOOLS_USE', use) as Promise<ToolStart>,
  });

/** *Download* (§4.1): fetched and verified, nothing switching. Answered once it has started; its end is `TOOLS_ENDED`. */
export const useToolDownload = () =>
  useMutation({
    mutationFn: (target: { tool: string; version: string }) => call('TOOLS_DOWNLOAD', target) as Promise<ToolStart>,
  });

/** The person's stop of a tool's download. `stopped: false` is an answer: nothing ran to stop. */
export const useToolStop = () =>
  useMutation({
    mutationFn: (tool: string) => call('TOOLS_STOP', { tool }) as Promise<{ tool: string; stopped: boolean }>,
  });

/** *Delete* (§3.6): a downloaded version nothing uses, removed with its folder. */
export const useToolDelete = () =>
  useMutation({
    mutationFn: (target: { tool: string; version: string }) =>
      call('TOOLS_DELETE', target) as Promise<{ tool: string; version: string }>,
  });

/**
 * *Look for updates*: each location fetched on the person's press. It waits as long as the shell may look,
 * a look's bound for each location (`hostBounds`), since the bridge's own thirty seconds is one location's.
 * The copy each location's look keeps is what the list offers, so its route publishes the tools.
 */
export const useToolsLook = () =>
  useMutation({
    mutationFn: (locations: number) =>
      call<{ looks: ToolLookShown[] }>('TOOLS_LOOK', undefined, { timeoutMs: toolLookBound(locations) }),
  });

/** A resource location added or removed (§3.3): the screen's half of `daoris tool locations add|remove`. */
export const useToolLocation = () =>
  useMutation({
    mutationFn: (change: { action: 'add' | 'remove'; address: string }) =>
      call('TOOLS_LOCATION', change) as Promise<{ address: string; added?: boolean; removed?: boolean }>,
  });

/**
 * A setting Daoris's git carries, set or cleared: the screen's half of `daoris tool git ssh`. No `value` clears it;
 * a git older than the one that reads it refuses as `TOOL_GIT_TOO_OLD`, naming both versions.
 */
export const useToolGitSet = () =>
  useMutation({
    mutationFn: (change: { key: 'core.sshCommand'; value?: string }) =>
      call('TOOLS_GIT_SET', change) as Promise<{ key: string; value: string | null; version: string | null }>,
  });

/** One act on a git identity, in the words `daoris git identity` uses. */
export type GitIdentityChange =
  | { action: 'add-ssh'; host: string; label: string; name?: string; email?: string; import?: string; rsa?: boolean }
  | { action: 'add-https'; host: string; label: string; user: string; token: string; name?: string; email?: string }
  | { action: 'token'; identity: string; kind?: 'api' | 'https'; token?: string; clear?: boolean }
  | { action: 'test'; identity: string; trust?: string; repository?: string }
  | { action: 'rename'; identity: string; label: string }
  | { action: 'commits-as'; identity: string; name?: string; email?: string; clear?: boolean }
  | { action: 'read-only'; identity: string; on: boolean }
  | { action: 'remove'; identity: string }
  | { action: 'default'; host: string; identity?: string };

/**
 * A git identity added, tested, changed or removed: the screen's half of `daoris git identity` and
 * `git default`. A refusal is the operation's own sentence. A token goes in and never comes back, only whether it is set; the press
 * that loosens what Daoris's git may use is confirmed in the page first.
 */
export const useGitIdentity = () =>
  useMutation({
    mutationFn: (change: GitIdentityChange) => call('GIT_IDENTITY', change) as Promise<Record<string, unknown>>,
  });

/** A choice of identity for a scope (§3.2): a repository's remote, a workspace's host or this machine's host; `own` or an id, or none clears it. */
export type GitUseChange =
  | { scope: 'repository'; repository: string; remote?: string; workspace?: string; value?: string }
  | { scope: 'workspace'; workspace: string; host: string; value?: string }
  | { scope: 'machine'; host: string; value?: 'own' };

/** The screen's half of `daoris git use`. Choosing `own` is a widening, confirmed in the page before it is sent. */
export const useSetGitUse = () =>
  useMutation({ mutationFn: (change: GitUseChange) => call('GIT_USE', change) as Promise<{ scope: string }> });

/**
 * A git go-ahead answered: the screen's half of `daoris git answer`. `waiting` is whether its door
 * still holds the act; where it has gone, an Allow is kept for the identical retry. Its row goes as the slice hears the answer.
 */
export const useGitAnswer = () =>
  useMutation({
    mutationFn: (answer: { ask: string; answer: 'allow' | 'refuse' }) =>
      call('GIT_ANSWER', answer) as Promise<{ ask: string; answer: string; waiting: boolean }>,
  });

/**
 * What a switch of git to `target` changes (§4.1), asked of both gits before the switch applies; null asks nothing.
 * Not retried: a refusal (a version not downloaded, a file not there) is the answer.
 */
export const useToolGit = (target: GitTarget | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: target ? gitSwitch(target) : ['tools', 'git', 'none'],
    queryFn: () => (target ? call('TOOLS_GIT', target) as Promise<GitSwitchShown> : Promise.reject(new Error('no git target'))),
    enabled: isAvailable && target !== null,
    retry: false,
  });
};

/**
 * *Browse…*: the system's file picker. It waits for as long as the dialog is open, as the folder picker does;
 * `file` absent is the person cancelling, and `can: false` a window with no picker.
 */
export const useToolPick = () =>
  useMutation({
    mutationFn: (title: string) => {
      // The dialog is owned by the window this page is in; the first main window names none.
      const window = ownWindowName(globalThis.location?.search ?? '');
      return call('TOOLS_PICK', window === null ? { title } : { title, window }, { timeoutMs: Infinity }) as Promise<{ can: boolean; file?: string | null }>;
    },
  });

/** A tool's action ended (§3.6): the list is asked again, and the handler says how it ended. */
export const useToolsEnded = (handler: (ended: ToolEndShown) => void) => {
  const client = useQueryClient();
  useShenoraEvent('DAORIS', 'TOOLS_ENDED', (payload) => {
    const ended = payload as ToolEndShown | undefined;
    if (!ended?.tool) return;
    void client.invalidateQueries({ queryKey: ALL_TOOLS });
    handler(ended);
  });
};
