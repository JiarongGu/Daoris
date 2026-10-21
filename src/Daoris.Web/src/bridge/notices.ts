import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { inCircle, keys, NOTICES_SLICE } from '../data/queries';
import { useScope } from '../data/scope';
import { call } from './call';
import { readSlice } from './driver';
import { mergeKeyed, readPushed, type SliceSpec } from './state/slices';

// Done and failed, as the shell pushes it (NOTIFY2f, D196 point 6; the notification design §2.2, §2.5): the notices of the machine's
// record `data/notices.json`, with whether each is read, judged in .NET once per process (`NoticesStateSlice`) and read alike in every
// window. The page orders them newest first, words them in the reader's language and keeps their acts.

/** Its partition of notices, each under its id. */
const NEWS = 'news';

/**
 * A notice as the shell pushes it: what it is, when, whether it is read, and what it is about. Its fields are the slice's allow-list,
 * which `NoticesStateSliceTests` reads from here.
 */
export type Notice = {
  id: string;
  /** `ended`, a session that ended without the person asking; `expired`, a terminal's press refused by its time. */
  kind: string;
  at: string;
  read: boolean;
  /** The workspace it was made in; none is every window's. */
  workspace?: string;
  session?: string;
  quest?: string;
  repository?: string;
  /** A session's end state. */
  state?: string;
  /** A press's act, as its catalogue key names it after `person.route.`. */
  route?: string;
  act?: string;
};

const keyed = (value: unknown): Record<string, unknown> | null =>
  typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as Record<string, unknown>) : null;

/** A pushed notice of a shape the page reads, or null: an id, a kind and a moment. */
export function noticeOf(value: unknown): Notice | null {
  const notice = keyed(value);
  if (!notice || typeof notice.id !== 'string' || typeof notice.kind !== 'string' || typeof notice.at !== 'string') return null;
  return { ...notice, read: notice.read === true } as Notice;
}

/** The partition composed: every notice newest first; nothing until the owner judged. */
export function composeNotices(partitions: ReadonlyMap<string, unknown>, judged: boolean): Notice[] | undefined {
  const news = keyed(partitions.get(NEWS));
  if (!judged || !news) return undefined;
  return Object.values(news)
    .map(noticeOf)
    .filter((notice): notice is Notice => notice !== null)
    .sort((a, b) => b.at.localeCompare(a.at) || a.id.localeCompare(b.id));
}

/** Done and failed as the shell pushes it: keyed by notice, so one notice read is one notice's delta. */
export const noticesSlice: SliceSpec = {
  name: NOTICES_SLICE,
  key: keys.notices,
  read: () => readSlice(NOTICES_SLICE),
  compose: composeNotices,
  merge: mergeKeyed,
};

/**
 * Done and failed in this window: none in a plain browser, which has no record, and none until the owner judged; then the notices of
 * the window's scope, its own workspace's and every machine-wide one, newest first.
 */
export const useNotices = (): Notice[] => {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const { workspace } = useScope();
  const read = useQuery({
    queryKey: keys.notices,
    queryFn: () => readPushed<Notice[] | null>(client, NOTICES_SLICE, async () => null),
    enabled: isAvailable,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
  if (!isAvailable || !read.data) return [];
  return read.data.filter((notice) => notice.workspace == null || inCircle(notice.workspace, workspace));
};

/** Whether a notice press went in: one refused (the record's lock held, a record this build will not write over) is the log's to say. */
const writtenOf = (answer: unknown): boolean => keyed(answer)?.written === true;

/**
 * The person's read of notices (`NOTICES_READ`): those shown in a window in front, or every one *Mark all read* names. Written by the
 * shell and pushed to every window by its slice.
 */
export const useMarkNoticesRead = () =>
  useMutation({
    mutationFn: async (ids: string[]): Promise<boolean> => writtenOf(await call<unknown>('NOTICES_READ', { ids })),
  });

/** The person's *Dismiss* of one notice (`NOTICE_DISMISS`): removed from every window by the slice's push. */
export const useDismissNotice = () =>
  useMutation({
    mutationFn: async (id: string): Promise<boolean> => writtenOf(await call<unknown>('NOTICE_DISMISS', { id })),
  });
