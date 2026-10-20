// An ask's channel as the shell answers it (SESSONE2c, D167 points 6 to 8; the co-working design §2.4): where an ask's
// sessions and the person talk, messages and Daoris's own coded lines in one time order. Pure, so every channel a person could
// have is an argument; the bridge (`bridge/sessions/asks.ts`) asks, and the molecules draw from these.

/**
 * One item. A `message` is words a session or the person wrote, addressed to everyone (`to` absent) or one repository, and
 * may answer another (`re`); a session's is that agent's claim, the person's is theirs (`person`). A `line` is Daoris's, read
 * from the ask's quests and never stored: a `code` the page words in the reader's language, with the quest and its name.
 * Fields a newer host adds are not read.
 */
export type ChannelItem = {
  kind: 'message' | 'line' | string;
  at: string;
  id?: string | null;
  person?: boolean;
  session?: string | null;
  repository?: string | null;
  agent?: string | null;
  /** The poster's line of work (D167 point 2), for a session's message. */
  entry?: string | null;
  to?: string | null;
  /** The machine that posted it, where it was not this one (SESSONE2d); absent for this machine's own. */
  origin?: string | null;
  re?: string | null;
  question?: boolean;
  /** A question no message answers yet. */
  open?: boolean;
  text?: string | null;
  code?: string | null;
  quest?: string | null;
  name?: string | null;
  from?: string | null;
  /**
   * When its sender asked it to reach a working session (COWORK1d, D188 point 2): true urgent, false ordinary; absent on a message
   * from before, which says no level.
   */
  urgent?: boolean | null;
  /** What it reached in each repository it addresses, as the person reads it (COWORK1d, the design §8.4); absent from a host from before. */
  receipts?: ChannelReceipt[] | null;
};

/**
 * A message's receipt in one repository it addresses (COWORK1d, the message-urgency design §7): `waiting`, `held`, `sent`, `taken`,
 * `read`, `partly`, `answered` or `not-reached`, never more than Daoris observed; answered is a reply, never that it was obeyed.
 */
export type ChannelReceipt = { repository: string; state: string };

/** The receipts the page has words for; another is said as the shell wrote it. */
export const RECEIPT_STATES = new Set(['waiting', 'held', 'sent', 'taken', 'read', 'partly', 'answered', 'not-reached']);

/**
 * Whether the person may send a message on to a repository's session from its receipt (COWORK1d, the design §4.2, §8.4): where it
 * waits there, is held for a turn's end, was handed only partly, or never reached it. Taken, read, sent and answered have reached it.
 */
export const forwardable = (receipt: ChannelReceipt): boolean =>
  receipt.state === 'waiting' || receipt.state === 'held' || receipt.state === 'partly' || receipt.state === 'not-reached';

/** The person's *Send to its session* as the shell answers it: sent, to which session, or the code that says why not. */
export type ChannelForwarded = { sent: boolean; code?: string | null; session?: string | null };

/** The shell's answer to *Send to its session*; one of another shape is a refusal with a code this page has no words for. */
export function forwardedOf(raw: unknown): ChannelForwarded {
  if (typeof raw !== 'object' || raw === null || typeof (raw as ChannelForwarded).sent !== 'boolean') {
    return { sent: false, code: 'other', session: null };
  }
  const { sent, code, session } = raw as ChannelForwarded;
  return { sent, code: typeof code === 'string' ? code : null, session: typeof session === 'string' ? session : null };
}

/**
 * How Daoris's words around another session's claim begin when the driver hands one to a running session (SESSONE2b,
 * `ChannelText.Claim`): the record keeps them as the driver's own words (`target`). Since SESSCTX1b the event carries the message's
 * own mark (`channel`), which the page reads first; this opening tells a claim on a record from before. A twin of the driver's fixed
 * opening, held by a test on the driver's side.
 */
export const CHANNEL_CLAIM_OPENING = "Another session says, on the ask's channel";

/** An ask's channel as the person reads it: their own mark, and how many items are past it, lines included. */
export type Channel = { ask: string; entry: string; items: ChannelItem[]; unread: number; mark?: string | null };

/** The person's post as the shell answers it: the message kept, or the service's code where it refused. */
export type ChannelPosted = { posted: boolean; code?: string | null; message?: ChannelItem | null };

/** What the page sends to post: their words, to one repository or everyone (absent), answering a message or not. */
export type ChannelPost = { text: string; to?: string | null; re?: string | null };

type Said = (key: string, options?: Record<string, unknown>) => string;

const isItem = (value: unknown): value is ChannelItem =>
  typeof value === 'object' && value !== null && typeof (value as ChannelItem).kind === 'string' && typeof (value as ChannelItem).at === 'string';

/**
 * The shell's answer as a channel, or null where it is not one (a shell from before the channel answers something else, and
 * then the section is not drawn). An item without its kind or its moment is passed over, as the driver's reader passes it.
 */
export function channelOf(raw: unknown): Channel | null {
  if (typeof raw !== 'object' || raw === null) return null;
  const { ask, entry, items, unread, mark } = raw as Record<string, unknown>;
  if (typeof ask !== 'string' || !Array.isArray(items)) return null;
  return {
    ask,
    entry: typeof entry === 'string' ? entry : 'person',
    items: items.filter(isItem),
    unread: typeof unread === 'number' ? unread : 0,
    mark: typeof mark === 'string' ? mark : null,
  };
}

/**
 * An ask the mirrored messages name and this machine does not hold (SESSONE2h): all this machine knows of a teammate's ask. There
 * is no record, so no title; `origins` are the machines its messages came from, and `unread` is against the person's local mark.
 */
export type ElsewhereAsk = { id: string; workspace: string; origins: string[]; latest: string; messages: number; unread: number };

/**
 * The shell's list of the asks held elsewhere, or null where the answer is not one (a shell from before the route answers
 * something else, and then no group is drawn). A row without its id or its moment is passed over.
 */
export function elsewhereOf(raw: unknown): ElsewhereAsk[] | null {
  if (typeof raw !== 'object' || raw === null || !Array.isArray((raw as { asks?: unknown }).asks)) return null;
  return (raw as { asks: unknown[] }).asks.flatMap((row): ElsewhereAsk[] => {
    if (typeof row !== 'object' || row === null) return [];
    const { id, workspace, origins, latest, messages, unread } = row as Record<string, unknown>;
    if (typeof id !== 'string' || typeof latest !== 'string') return [];
    return [{
      id,
      workspace: typeof workspace === 'string' ? workspace : '',
      origins: Array.isArray(origins) ? origins.filter((origin): origin is string => typeof origin === 'string') : [],
      latest,
      messages: typeof messages === 'number' ? messages : 0,
      unread: typeof unread === 'number' ? unread : 0,
    }];
  });
}

/** The person's post as the shell answered it; an answer of another shape is a refusal with a code this page has no words for. */
export function postedOf(raw: unknown): ChannelPosted {
  if (typeof raw !== 'object' || raw === null || typeof (raw as ChannelPosted).posted !== 'boolean') {
    return { posted: false, code: 'other', message: null };
  }
  const { posted, code, message } = raw as ChannelPosted;
  return { posted, code: typeof code === 'string' ? code : null, message: isItem(message) ? message : null };
}

/** A repository's name as the service matches it: without case (it stores the registry's spelling). */
const same = (a: string | null | undefined, b: string | null | undefined) =>
  a != null && b != null && a.toLowerCase() === b.toLowerCase();

/**
 * The repositories a message may be addressed to, as far as the channel shows them: those it names in its lines and
 * messages, after any the page knows (the ask's own lines of work), once each, in the order met. An asker that is the ask
 * itself (`ask #id`) is no repository.
 */
export function repositoriesOf(items: readonly ChannelItem[], known: readonly string[] = []): string[] {
  const found: string[] = [];
  const add = (name: string | null | undefined) => {
    if (name && !name.startsWith('ask #') && !found.some((held) => same(held, name))) found.push(name);
  };
  known.forEach(add);
  for (const item of items) {
    if (item.kind === 'line') {
      add(item.to);
      add(item.from);
    } else {
      add(item.repository);
      add(item.to);
    }
  }
  return found;
}

/**
 * What a session's panel holds of the channel (design §2.4): the messages to everyone, to its repository and by its own line
 * of work, and the lines of the quests that concern its repository (asked of it, or by it).
 */
export function forEntry(items: readonly ChannelItem[], { entry, repository }: { entry: string; repository: string }): ChannelItem[] {
  return items.filter((item) => item.kind === 'line'
    ? same(item.to, repository) || same(item.from, repository)
    : item.entry === entry || item.to == null || same(item.to, repository));
}

/** The message a message answers, or null where it answers none or the channel does not hold it. */
export function repliesTo(items: readonly ChannelItem[], re: string | null | undefined): ChannelItem | null {
  return re ? items.find((item) => item.kind === 'message' && item.id === re) ?? null : null;
}

/** The newest moment among the items, or null for none. */
export function newestAt(items: readonly ChannelItem[]): string | null {
  return items.reduce<string | null>((newest, item) => (newest === null || Date.parse(item.at) > Date.parse(newest) ? item.at : newest), null);
}

/** The items after a mark, or all of them where none has been moved. */
export function sinceMark(items: readonly ChannelItem[], mark: string | null | undefined): ChannelItem[] {
  return mark ? items.filter((item) => Date.parse(item.at) > Date.parse(mark)) : [...items];
}

/** The unread counts of several asks added up, for a heading over them. */
export const unreadSum = (counts: Readonly<Record<string, number>>): number => Object.values(counts).reduce((sum, n) => sum + n, 0);

/** The codes Daoris's lines carry that this page has words for (the service's `ChannelLine.Code`). */
const LINE_CODES = new Set(['published', 'taken', 'done', 'declined', 'awaiting']);

/**
 * One of Daoris's lines in the reader's language, from its code (D142): the quest by the name the list gives it, else by its
 * id, and the repository it concerns. A code this page has no words for is said as the service wrote it.
 */
export function lineWords(t: Said, line: ChannelItem): string {
  const quest = line.name || (line.quest ? `#${line.quest}` : '');
  const code = line.code ?? '';
  return LINE_CODES.has(code)
    ? t(`asks.channel.line.${code}`, { quest, to: line.to ?? '' })
    : t('asks.channel.line.other', { code, quest });
}
