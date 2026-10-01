import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora, useShenoraEvent } from '@shenora/react';
import type {
  GitSwitchShown, ToolEndShown, ToolLookShown, ToolsShown, ToolWay,
} from '../settings/Tools';
import { call, toolLookBound } from './call';

// The programs Daoris runs beside its agents (MOD3, TOOLS7): how each is run, a version downloaded and followed, a
// delete, the resource locations, and what a switch of git changes (D121 §4.1). The same `tools.json` the terminal's
// `daoris tool` edits (D50).

/**
 * The tools' own query keys, under a root of their own rather than the driver's: the driver's state is asked again on
 * every tick, and the list with versions starts each program. Kept here, as Ask Daoris's proposals keep theirs, so
 * this domain's change touches no shared file.
 */
const TOOLS = ['tools'] as const;
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
 * Every tool as it is run, what is downloaded and offered, and the locations (TOOLS7). Shell-only: a tool's file is a
 * machine path (D47 §4). Without `ask` it reads files and answers at once; with it, the shell asks each tool's file its
 * version, which starts the program, so the screen draws from the first and fills in from the second.
 */
export const useTools = ({ ask = false }: { ask?: boolean } = {}) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: toolsList(ask),
    queryFn: () => call<ToolsShown>('TOOLS_LIST', ask ? { ask: true } : undefined),
    enabled: isAvailable,
  });
};

/**
 * The screen's half of `daoris tool use <tool> system|managed|file` (D50): the system's and a named file are written at
 * once, a named file only once it answers a version; managed downloads a version that is not here, then switches, and
 * is followed as a download is.
 */
export const useToolUse = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (use: { tool: string; action: 'system' | 'managed' | 'file'; version?: string; file?: string }) =>
      call<ToolStart>('TOOLS_USE', use),
    onSuccess: () => void client.invalidateQueries({ queryKey: TOOLS }),
  });
};

/** *Download* (§4.1): fetched and verified, nothing switching. Answered once it has started; its end is `TOOLS_ENDED`. */
export const useToolDownload = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (target: { tool: string; version: string }) => call<ToolStart>('TOOLS_DOWNLOAD', target),
    onSuccess: () => void client.invalidateQueries({ queryKey: TOOLS }),
  });
};

/** The person's stop of a tool's download. `stopped: false` is an answer: nothing ran to stop. */
export const useToolStop = () =>
  useMutation({
    mutationFn: (tool: string) => call<{ tool: string; stopped: boolean }>('TOOLS_STOP', { tool }),
  });

/** *Delete* (§3.6): a downloaded version nothing uses, removed with its folder. */
export const useToolDelete = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (target: { tool: string; version: string }) =>
      call<{ tool: string; version: string }>('TOOLS_DELETE', target),
    onSuccess: () => void client.invalidateQueries({ queryKey: TOOLS }),
  });
};

/**
 * *Look for updates* (§3.7): each location fetched on the person's press. It waits as long as the shell may look,
 * a look's bound for each location (`hostBounds`, WSR7), since the bridge's own thirty seconds is one location's.
 */
export const useToolsLook = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (locations: number) =>
      call<{ looks: ToolLookShown[] }>('TOOLS_LOOK', undefined, { timeoutMs: toolLookBound(locations) }),
    onSuccess: () => void client.invalidateQueries({ queryKey: TOOLS }),
  });
};

/** A resource location added or removed (§3.3): the screen's half of `daoris tool locations add|remove`. */
export const useToolLocation = () => {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (change: { action: 'add' | 'remove'; address: string }) =>
      call<{ address: string; added?: boolean; removed?: boolean }>('TOOLS_LOCATION', change),
    onSuccess: () => void client.invalidateQueries({ queryKey: TOOLS }),
  });
};

/**
 * What a switch of git to `target` changes (§4.1), asked of both gits before the switch applies; null asks nothing.
 * Not retried: a refusal (a version not downloaded, a file not there) is the answer.
 */
export const useToolGit = (target: GitTarget | null) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: target ? gitSwitch(target) : ['tools', 'git', 'none'],
    queryFn: () => call<GitSwitchShown>('TOOLS_GIT', target ?? undefined),
    enabled: isAvailable && target !== null,
    retry: false,
  });
};

/**
 * *Browse…* (§4.1): the system's file picker. It waits for as long as the dialog is open, as the folder picker does
 * (SHEN2); `file` absent is the person cancelling, and `can: false` a window with no picker.
 */
export const useToolPick = () =>
  useMutation({
    mutationFn: (title: string) =>
      call<{ can: boolean; file?: string | null }>('TOOLS_PICK', { title }, { timeoutMs: Infinity }),
  });

/** A tool's action ended (§3.6): the list is asked again, and the handler says how it ended. */
export const useToolsEnded = (handler: (ended: ToolEndShown) => void) => {
  const client = useQueryClient();
  useShenoraEvent('DAORIS', 'TOOLS_ENDED', (payload) => {
    const ended = payload as ToolEndShown | undefined;
    if (!ended?.tool) return;
    void client.invalidateQueries({ queryKey: TOOLS });
    handler(ended);
  });
};
