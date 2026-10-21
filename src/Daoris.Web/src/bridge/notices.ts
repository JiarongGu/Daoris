import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useShenora } from '@shenora/react';
import { inCircle, keys, NOTICES_SLICE } from '../data/queries';
import { useScope } from '../data/scope';
import { call } from './call';
import { readSlice } from './driver';
import { mergeKeyed, readPushed, type SliceSpec } from './state/slices';

// Done and failed, as the shell pushes it (NOTIFY2f, D196 point 6; the notification design §2.2, §2.5): the notices of the machine's
// record `data/notices.json`, with whether each is read, judged in .NET once per process (`NoticesStateSlice`) and read alike in every
// window. The page orders them newest first, words them in the reader's language and keeps their acts. Beside them, Messages
// (NOTIFY2g): each ask's unread count as the service keeps the person's mark, with its newest message from an agent or a teammate,
// and a conversation's answer, a notice of its own kind.

/** Its partition of notices, each under its id. */
const NEWS = 'news';

/** Its partition of Messages, each live ask's and each teammate's under the ask's id (NOTIFY2g). */
const MESSAGES = 'messages';

/** The notice of a conversation that answered the person's words (NOTIFY2g): Messages', never Done and failed's. */
export const ANSWERED = 'answered';

/**
 * A notice as the shell pushes it: what it is, when, whether it is read, and what it is about. Its fields are the slice's allow-list,
 * which `NoticesStateSliceTests` reads from here.
 */
export type Notice = {
  id: string;
  /**
   * `ended`, a session that ended without the person asking; `expired`, a terminal's press refused by its time; `answered`, a
   * conversation that answered the person's words (NOTIFY2g), one per conversation.
   */
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

/**
 * The newest message on an ask's channel written by an agent or a teammate, never the person's own and never a Daoris line, read or
 * not (NOTIFY2g). Its fields are the slice's allow-list, which `NoticesStateSliceTests` reads from here.
 */
export type NewestMessage = {
  id: string;
  at: string;
  /** An agent's, named by its repository; or a teammate's. */
  who: 'agent' | 'teammate';
  repository?: string;
  /** Its first line, bounded by the shell, shown as written. */
  line: string;
  /** D188's level; one from before the levels is `ordinary`. */
  level: 'urgent' | 'ordinary';
  toEveryone: boolean;
};

/**
 * An ask's entry in Messages as the shell pushes it (NOTIFY2g, the notification design §2.5): every live local ask and every
 * teammate's, `unread: 0` among them. Its fields are the slice's allow-list, which `NoticesStateSliceTests` reads from here.
 */
export type AskMessages = {
  ask: string;
  workspace: string;
  /** A teammate's ask (`ASKS_ELSEWHERE`'s), held on their machine. */
  teammate: boolean;
  /** The service's own count past the person's mark: the number the ask's row says. */
  unread: number;
  /** Absent where no agent and no teammate wrote on it. */
  newest?: NewestMessage;
};

/** An urgent message to everyone as the shell says it arrived (`MESSAGE`): the message and the ask it is on. */
export type MessageArrived = { ask: string; workspace: string; teammate: boolean; newest: NewestMessage };

/** What the slice's one entry holds: the notices newest first, and Messages where the shell pushes the partition. */
export type NoticesRead = { news: Notice[]; messages?: AskMessages[] };

const LEVELS = new Set(['urgent', 'ordinary']);

/** A pushed newest message of a shape the page reads, or null: an id, a moment, who wrote it and its line. */
function newestOf(value: unknown): NewestMessage | null {
  const newest = keyed(value);
  if (!newest || typeof newest.id !== 'string' || typeof newest.at !== 'string' || typeof newest.line !== 'string') return null;
  return {
    id: newest.id,
    at: newest.at,
    who: newest.who === 'teammate' ? 'teammate' : 'agent',
    ...(typeof newest.repository === 'string' && newest.repository ? { repository: newest.repository } : {}),
    line: newest.line,
    level: LEVELS.has(newest.level as string) ? (newest.level as NewestMessage['level']) : 'ordinary',
    toEveryone: newest.toEveryone === true,
  };
}

/** A pushed ask's entry of a shape the page reads, or null: its ask and its count. */
export function askMessagesOf(value: unknown): AskMessages | null {
  const entry = keyed(value);
  if (!entry || typeof entry.ask !== 'string' || typeof entry.unread !== 'number') return null;
  const newest = newestOf(entry.newest);
  return {
    ask: entry.ask,
    workspace: typeof entry.workspace === 'string' ? entry.workspace : '',
    teammate: entry.teammate === true,
    unread: Math.max(0, entry.unread),
    ...(newest ? { newest } : {}),
  };
}

/** A `MESSAGE` arrival of a shape the page reads, or null: the message's fields beside its ask's. */
export function messageOf(value: unknown): MessageArrived | null {
  const arrived = keyed(value);
  const newest = newestOf(arrived);
  if (!arrived || !newest || typeof arrived.ask !== 'string') return null;
  return {
    ask: arrived.ask,
    workspace: typeof arrived.workspace === 'string' ? arrived.workspace : '',
    teammate: arrived.teammate === true,
    newest,
  };
}

/**
 * The partitions composed: every notice newest first, and each ask's entry by its newest message, newest first; nothing until the
 * owner judged. A shell older than Messages pushes no such partition, and composes none, so the page pulls each channel as before.
 */
export function composeNotices(partitions: ReadonlyMap<string, unknown>, judged: boolean): NoticesRead | undefined {
  const news = keyed(partitions.get(NEWS));
  if (!judged || !news) return undefined;
  const messages = keyed(partitions.get(MESSAGES));
  return {
    news: Object.values(news)
      .map(noticeOf)
      .filter((notice): notice is Notice => notice !== null)
      .sort((a, b) => b.at.localeCompare(a.at) || a.id.localeCompare(b.id)),
    ...(messages
      ? {
        messages: Object.values(messages)
          .map(askMessagesOf)
          .filter((entry): entry is AskMessages => entry !== null)
          .sort((a, b) => (b.newest?.at ?? '').localeCompare(a.newest?.at ?? '') || a.ask.localeCompare(b.ask)),
      }
      : {}),
  };
}

/** Done and failed and Messages as the shell pushes them: keyed by notice and by ask, so one read is one key's delta. */
export const noticesSlice: SliceSpec = {
  name: NOTICES_SLICE,
  key: keys.notices,
  read: () => readSlice(NOTICES_SLICE),
  compose: composeNotices,
  merge: mergeKeyed,
};

/** The slice's entry as this window reads it: none in a plain browser, which has no record, and none until the owner judged. */
function useNoticesRead(): NoticesRead | null {
  const { isAvailable } = useShenora();
  const client = useQueryClient();
  const read = useQuery({
    queryKey: keys.notices,
    queryFn: () => readPushed<NoticesRead | null>(client, NOTICES_SLICE, async () => null),
    enabled: isAvailable,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
  return isAvailable ? read.data ?? null : null;
}

/** Whether a record of `workspace` is in the window's scope: none is every window's (§2.4). */
const scoped = (workspace: string | null | undefined, scope: string | null) => workspace == null || inCircle(workspace, scope);

/**
 * Done and failed in this window: none in a plain browser, which has no record, and none until the owner judged; then the notices of
 * the window's scope, its own workspace's and every machine-wide one, newest first. A conversation's answer is Messages', never here.
 */
export const useNotices = (): Notice[] => {
  const { workspace } = useScope();
  const read = useNoticesRead();
  if (!read) return [];
  return read.news.filter((notice) => notice.kind !== ANSWERED && scoped(notice.workspace, workspace));
};

/**
 * Every ask's entry in Messages as the shell pushes it, whatever the window's scope: undefined where the slice is not live in this
 * window or the shell pushes no such partition, which then keeps each channel's pull (`useChannelUnreads`).
 */
export const useAskMessages = (): AskMessages[] | undefined => useNoticesRead()?.messages;

/**
 * Messages in this window (NOTIFY2g, the notification design §2.2's third group): each ask's entry of the window's scope, its own
 * workspace's and every machine-wide one, `undefined` where the shell pushes no such partition; and the conversations' answers of
 * its scope, newest first. Being listed reads none of them.
 */
export const useMessages = (): { asks: AskMessages[] | undefined; answered: Notice[] } => {
  const { workspace } = useScope();
  const read = useNoticesRead();
  return {
    asks: read?.messages?.filter((entry) => scoped(entry.workspace || null, workspace)),
    answered: (read?.news ?? []).filter((notice) => notice.kind === ANSWERED && scoped(notice.workspace, workspace)),
  };
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
