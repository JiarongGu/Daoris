import type { SaidWord } from '../../data/api';
import type { SessionEvent } from './conversation';

/**
 * Who said a word, read once from an event's marks, or a kept word's, and never stored. This is the
 * page's twin of the service's `Senders` and the driver's, held with them to `fixtures/senders.json` among the service's tests, row for row.
 *
 * `composed` is an event's only: the target Daoris composed, which carries no mark of a sender.
 */
export const SENDERS = [
  'person', 'daoris', 'supervisor', 'channel', 'helper', 'partner', 'opinion', 'unattributed', 'composed',
] as const;
export type Sender = (typeof SENDERS)[number];

/** Whose words they are where a heading of their own says it: Daoris, the supervisor, a helper, a partner, or an author not known. */
export type Author = Extract<Sender, 'daoris' | 'supervisor' | 'helper' | 'partner' | 'unattributed'>;

/**
 * Whether the record a helper's word names is a partner: by the work a host sends it, and by the piece it serves where a host sends
 * none (an event carries no key to tell them by, B11).
 */
export const isPartnerRecord = (record: { work?: string | null; serves?: string | null; piece?: string | null } | undefined): boolean =>
  record?.work === 'partner' || (!record?.work && Boolean(record?.serves) && Boolean(record?.piece));

/**
 * The sender of a `user` event. `helperWork` is the work of the record the event's `helper` names, or null where the page does not hold
 * that record, which reads as a helper's.
 */
export function senderOf(
  event: Pick<SessionEvent, 'origin' | 'from' | 'opinion' | 'helper' | 'channel'>, helperWork?: string | null,
): Sender {
  const target = event.origin === 'target';
  if (event.from) {
    if (event.from === 'supervisor') return 'supervisor';
    // A `daoris` mark on the person's own origin reads as theirs, as it always did.
    if (event.from === 'daoris') return target ? 'daoris' : 'person';
    return event.from === 'agent' ? 'channel' : 'unattributed';
  }
  if (event.opinion) return 'opinion';
  if (event.helper) return helperWork === 'partner' ? 'partner' : 'helper';
  if (target) return event.channel ? 'channel' : 'composed';
  return 'person';
}

/**
 * The sender of a word kept on a record, by its marks as the service's `Senders.Of` reads them (a park's answer is read from
 * these words): Daoris's mark is its own here, never the person's, and a helper's word with a `partner:` key is a partner's.
 */
export function wordSenderOf(word: Pick<SaidWord, 'from' | 'by' | 'helper' | 'key'>): Sender {
  if (word.from) {
    if (word.from === 'supervisor') return 'supervisor';
    if (word.from === 'daoris') return 'daoris';
    return word.from === 'agent' ? 'channel' : 'unattributed';
  }
  if (word.by) return 'opinion';
  if (word.helper) return word.key?.startsWith('partner:') ? 'partner' : 'helper';
  return 'person';
}

const AUTHORS: ReadonlySet<Sender> = new Set<Sender>(['daoris', 'supervisor', 'helper', 'partner', 'unattributed']);

/** The {@link Author} of a word event, or undefined for the person's, a channel post, a second opinion and the composed target. */
export function authorOf(
  event: Pick<SessionEvent, 'origin' | 'from' | 'opinion' | 'helper' | 'channel'>, helperWork?: string | null,
): Author | undefined {
  const sender = senderOf(event, helperWork);
  return AUTHORS.has(sender) ? (sender as Author) : undefined;
}
