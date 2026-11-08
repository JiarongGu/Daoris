import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import type { FilesLook, FilesPress } from '../../projects/files/filesLook';
import { call } from '../call';
import { askHeld, isPending } from '../state/held';

// A repository's files git does not manage: the rule file's layers read for its page, and the person's own rules and
// permissions written there. Every route is the window's alone; a terminal's twin is `daoris files`.

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

/** The person's presses, each the window's alone; the state hub asks the section again in every window. */
export const useFilesPress = (repository: string) =>
  useMutation({
    mutationFn: (press: FilesPress): Promise<FilesAnswer> => {
      switch (press.act) {
        case 'read': return call('FILES_READ', { repository }) as Promise<FilesAnswer>;
        case 'set': return call('FILES_SET', { repository, kind: press.kind, paths: press.paths, copy: press.copy, machine: press.machine }) as Promise<FilesAnswer>;
        case 'remove': return call('FILES_REMOVE', { repository, paths: press.paths, machine: press.machine }) as Promise<FilesAnswer>;
        case 'settle': return call('FILES_SETTLE', { repository, wordings: press.wordings }) as Promise<FilesAnswer>;
        case 'permit': return call('FILES_PERMIT', { repository, act: press.change, list: press.list, rule: press.rule, machine: press.machine }) as Promise<FilesAnswer>;
      }
    },
  });
