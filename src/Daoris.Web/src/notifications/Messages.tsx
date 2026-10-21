import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import type { AskMessages, Notice } from '../bridge/notices';
import { HELP_REPOSITORY } from '../data/api';
import { Button, Pill } from '../design/ui';
import { elapsed } from '../locales/format';

/** What names an ask or a conversation in Messages: the ask's own sentence where this machine holds it, a conversation's title. */
export type MessageNames = {
  /** An ask this machine holds, by its name (`askName`); null where it holds none. */
  ask?: (id: string) => string | null;
  /** A conversation by its title; null where none is known. */
  session?: (id: string) => string | null;
};

/** The asks Messages lists, one item each: those with anything past the person's mark. */
export const unreadAsks = (asks: readonly AskMessages[]) => asks.filter((entry) => entry.unread > 0);

/** The answers Messages lists: each unread one; one read has nothing left to say. */
export const unreadAnswers = (answered: readonly Notice[]) => answered.filter((notice) => !notice.read);

/**
 * Messages in a count (§2.2, §4): the items it lists, one per ask and one per answer, and how many are unread, each ask's by the
 * service's count and each answer once. The view's head, Overview's line and the bell's dot read this one sum.
 */
export function messagesCount(asks: readonly AskMessages[], answered: readonly Notice[]): { items: number; unread: number } {
  const listed = unreadAsks(asks);
  const answers = unreadAnswers(answered);
  return {
    items: listed.length + answers.length,
    unread: listed.reduce((sum, entry) => sum + entry.unread, 0) + answers.length,
  };
}

/** What names an ask: its own sentence where this machine holds it; a teammate's said as such, with its id. */
export function askWords(entry: Pick<AskMessages, 'ask' | 'teammate'>, t: TFunction, names?: MessageNames): string {
  if (entry.teammate) return t('asks.elsewhere.title', { id: entry.ask });
  return names?.ask?.(entry.ask) ?? t('notifications.messages.ask', { id: entry.ask });
}

/** Who wrote a message: an agent by its repository, else a session; a teammate as one. */
export function whoWords(newest: NonNullable<AskMessages['newest']>, t: TFunction): string {
  if (newest.who === 'teammate') return t('notifications.messages.teammate');
  return newest.repository || t('asks.channel.who.session');
}

/** A conversation's answer in the reader's language (§2.2): by its title where one is known, Ask Daoris by its name. */
export function answeredWords(notice: Notice, t: TFunction, names?: MessageNames): string {
  if (notice.repository === HELP_REPOSITORY) return t('notifications.messages.answeredHelp');
  const title = notice.session ? names?.session?.(notice.session) : null;
  if (title) return t('notifications.messages.answeredNamed', { conversation: title });
  return t('notifications.messages.answered', { repository: notice.repository ?? '' });
}

/** One item of Messages: an ask's, or a conversation's answer, ordered by its moment. */
type Item = { key: string; at: string } & ({ ask: AskMessages } | { answer: Notice });

/**
 * *Messages* (NOTIFY2g, D196, the notification design §2.2's third group, §2.3, §2.4): one item per ask with anything past the
 * person's mark, a teammate's among them, and one per conversation that answered them and is unread, newest first.
 *
 * @remarks
 * **An ask's item** says what names the ask, the service's own count (the number its row says), and the newest message's first line
 * with who wrote it, an agent by its repository or a teammate; the line is shown as written. *Open* goes to the ask's channel on its
 * page, where reading moves the person's mark; *Mark read* moves the same mark through the newest message (`CHANNEL_MARK`). Nothing
 * is answered here: a reply needs its thread and its receivers.
 *
 * **A conversation's answer** has no channel and no mark: *Open* goes to the conversation and reads it, *Mark read* reads it
 * (`NOTICES_READ`), and so does its conversation attended in a window in front (`useAnsweredRead`).
 *
 * **Being listed reads nothing** (§2.4): an item shows one line of several. The group's *Mark all read* marks every ask and answer
 * it lists. A molecule: the entries come in, the doors and the marks go out.
 */
export function Messages({ asks, answered, names, onOpenAsk, onMarkAsk, onOpenAnswer, onRead, now = Date.now() }: {
  /** The window's asks' entries; those with nothing unread are not listed. */
  asks: readonly AskMessages[];
  /** The window's answers; those read are not listed. */
  answered: readonly Notice[];
  names?: MessageNames;
  /** An ask's door: its channel on its page. */
  onOpenAsk: (entry: AskMessages) => void;
  /** An ask's *Mark read*: its mark moved through its newest message. */
  onMarkAsk: (entry: AskMessages) => void;
  /** An answer's door: its conversation; absent where there is nowhere to open it. */
  onOpenAnswer?: (notice: Notice) => void;
  /** The answers read by *Open*, *Mark read* or *Mark all read*. */
  onRead: (ids: string[]) => void;
  /** The clock its ages are read by. */
  now?: number;
}) {
  const { t } = useTranslation();
  const listed = unreadAsks(asks);
  const answers = unreadAnswers(answered);
  const items: Item[] = [
    ...listed.map((entry): Item => ({ key: `ask:${entry.ask}`, at: entry.newest?.at ?? '', ask: entry })),
    ...answers.map((notice): Item => ({ key: notice.id, at: notice.at, answer: notice })),
  ].sort((a, b) => b.at.localeCompare(a.at) || a.key.localeCompare(b.key));
  const all = () => {
    for (const entry of listed) onMarkAsk(entry);
    if (answers.length > 0) onRead(answers.map((notice) => notice.id));
  };
  const age = (at: string) => t('notifications.done.ago', { age: elapsed(at, new Date(now).toISOString()) });
  return (
    <div className="grid gap-1.5">
      {items.length > 0 && (
        <div className="flex justify-end">
          <Button variant="ghost" onClick={all}>{t('notifications.messages.markAll')}</Button>
        </div>
      )}
      <ul className="m-0 grid list-none gap-1.5 p-0">
        {items.map((item) => ('ask' in item
          ? (
            <li key={item.key} data-message-ask={item.ask.ask} className="grid gap-1 rounded-sm border border-line bg-raised px-2.5 py-2">
              <p className="m-0 flex items-baseline gap-1.5 text-body text-ink">
                <span data-message-name="" className="min-w-0 flex-1 wrap-anywhere">{askWords(item.ask, t, names)}</span>
                <span className="shrink-0 font-mono text-meta tabular-nums text-ink-open">
                  {t('notifications.messages.unread', { count: item.ask.unread })}
                </span>
              </p>
              {item.ask.newest && (
                <p className="m-0 grid gap-0.5 text-small">
                  <span className="flex items-baseline gap-1.5 text-ink-faint">
                    <span className="min-w-0 truncate">{whoWords(item.ask.newest, t)}</span>
                    {item.ask.newest.level === 'urgent' && <Pill tone="open">{t('work.level.urgent')}</Pill>}
                  </span>
                  {/* What an agent or a teammate wrote, as written (§6). */}
                  <span className="line-clamp-2 wrap-anywhere text-ink-soft">{item.ask.newest.line}</span>
                </p>
              )}
              <div className="flex items-center gap-2">
                <span className="min-w-0 flex-1 text-small text-ink-faint">{item.ask.newest ? age(item.ask.newest.at) : null}</span>
                <Button variant="ghost" onClick={() => onOpenAsk(item.ask)}>{t('notifications.toast.open')}</Button>
                <Button variant="ghost" onClick={() => onMarkAsk(item.ask)}>{t('notifications.markRead')}</Button>
              </div>
            </li>
          )
          : (
            <li key={item.key} data-notice={item.answer.id} className="grid gap-1 rounded-sm border border-line bg-raised px-2.5 py-2">
              <p className="m-0 flex items-baseline gap-1.5 text-body text-ink">
                <span role="img" aria-label={t('notifications.done.unread')} className="inline-block h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
                <span data-message-name="" className="min-w-0 flex-1">{answeredWords(item.answer, t, names)}</span>
              </p>
              <div className="flex items-center gap-2">
                <span className="min-w-0 flex-1 text-small text-ink-faint">{age(item.answer.at)}</span>
                {onOpenAnswer && item.answer.session && (
                  <Button variant="ghost" onClick={() => { onRead([item.answer.id]); onOpenAnswer(item.answer); }}>
                    {t('notifications.toast.open')}
                  </Button>
                )}
                <Button variant="ghost" onClick={() => onRead([item.answer.id])}>{t('notifications.markRead')}</Button>
              </div>
            </li>
          )))}
      </ul>
    </div>
  );
}
