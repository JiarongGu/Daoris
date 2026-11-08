import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { call } from '../call';
import { askHeld, isPending } from '../state/held';

// A repository's files git does not manage: the rule file's layers read for its page, and the person's own rules and
// permissions written there. Every route is the window's alone; a terminal's twin is `daoris files`.

/** The layers a rule can come from, in the order they are read; the floor is read after all of them. */
export type FilesLayer = 'found' | 'repository' | 'machine' | 'person' | 'floor';

/** The kinds of file a rule names. */
export type FilesKind = 'dependency' | 'intermediate' | 'output' | 'cache' | 'local' | 'scratch' | 'repository' | 'unknown';

export const FILES_KINDS: readonly FilesKind[] = ['dependency', 'intermediate', 'output', 'cache', 'local', 'scratch', 'repository', 'unknown'];

/** What a layer below the winning one said of a path. */
export type FilesBelow = { layer: FilesLayer; kind: FilesKind };

/** One path git does not manage: its effective rule, the layer that decided, what the walk counted and what the layers below said. */
export type FilesFile = {
  path: string;
  /** Absent where git tracks the path. */
  kind?: FilesKind | null;
  /** Absent where no rule names the path. */
  layer?: FilesLayer | null;
  floor?: string | null;
  copy?: boolean;
  idle?: string | null;
  files?: number | null;
  bytes?: number | null;
  more?: boolean;
  evidence?: string[];
  below?: FilesBelow[];
};

/** An act the rules would make in a tree and the person's answer to it. */
export type FilesAct = { act: string; path: string; verdict: 'made' | 'denied' | 'held' };

export type FilesAllow = { rule: string; by?: string | null };

/** One scope of the person's permissions. */
export type FilesPermissions = { scope: 'machine' | 'repository'; deny: string[]; ask: string[]; allow: FilesAllow[] };

/** One rule of the person's own. */
export type FilesRule = { scope: 'machine' | 'repository'; kind: FilesKind; paths: string[]; copy?: boolean | null };

/** `FILES`'s answer for a repository, never a machine path. */
export type FilesLook = {
  repository: string;
  commit: string;
  foundCommit?: string | null;
  foundAt?: string | null;
  files: FilesFile[];
  acts: FilesAct[];
  permissions: FilesPermissions[];
  rules: FilesRule[];
  /** Why the person's file stands and does not read, where it does not. */
  unread?: string | null;
  problems: string[];
};

/** What a press answers: done, or why not by a code the page words, with the driver's sentence. */
export type FilesAnswer = { done: boolean; why?: 'unread' | 'unchanged' | 'refused' | 'no-checkout' | 'unreadable' | null; message?: string | null };

/** The reading, or why there is none (a repository with no checkout here, or one git could not read). */
export type FilesReading = { look: FilesLook | null; refusal: 'no-checkout' | 'unreadable' | null };

const filesKey = (repository: string) => ['files', repository] as const;

/** A repository's files, told by event (git and the walks answer it) and asked again when the person's rules move. */
export const useFiles = (repository: string, enabled = true) => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const queryKey = filesKey(repository);
  return useQuery({
    queryKey,
    queryFn: () => askHeld('FILES', client, queryKey, { repository },
      (answer): FilesReading | null => {
        if (isPending(answer)) return null;
        const read = answer as { look?: FilesLook; refusal?: FilesReading['refusal'] };
        return { look: read.look ?? null, refusal: read.refusal ?? null };
      }),
    enabled: isAvailable && enabled,
  });
};

/** The presses of the section: read again, the person's rules set and removed, a permission added or taken back. */
export type FilesPress =
  | { act: 'read' }
  | { act: 'set'; kind: FilesKind; paths: string[]; copy?: boolean; machine?: boolean }
  | { act: 'remove'; paths: string[]; machine?: boolean }
  | { act: 'permit'; change: 'add' | 'remove'; list: 'deny' | 'ask' | 'allow'; rule: string; machine?: boolean };

/** The person's presses, each the window's alone; the state hub asks the section again in every window. */
export const useFilesPress = (repository: string) =>
  useMutation({
    mutationFn: (press: FilesPress): Promise<FilesAnswer> => {
      switch (press.act) {
        case 'read': return call('FILES_READ', { repository }) as Promise<FilesAnswer>;
        case 'set': return call('FILES_SET', { repository, kind: press.kind, paths: press.paths, copy: press.copy, machine: press.machine }) as Promise<FilesAnswer>;
        case 'remove': return call('FILES_REMOVE', { repository, paths: press.paths, machine: press.machine }) as Promise<FilesAnswer>;
        case 'permit': return call('FILES_PERMIT', { repository, act: press.change, list: press.list, rule: press.rule, machine: press.machine }) as Promise<FilesAnswer>;
      }
    },
  });
