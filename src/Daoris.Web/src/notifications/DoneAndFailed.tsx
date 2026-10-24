import type { TFunction } from 'i18next';
import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import type { Notice } from '../bridge/notices';
import { Button } from '../design/ui';
import { cn } from '../lib/cn';
import { elapsed } from '../locales/format';
import { useShownOnce } from '../person/Confirmations';

/**
 * A notice's one line in the reader's language (§2.2): what ended and how, which press went unanswered, or what a look found that is
 * news (NOTIFY2h). Never a raw state or code.
 */
export function noticeWords(notice: Notice, t: TFunction): string {
  if (notice.kind === 'ended') {
    const state = notice.state ? t(`sessionState.${notice.state}`, { defaultValue: '' }) : '';
    return t('notifications.done.ended', { repository: notice.repository ?? '', state: state || t('notifications.done.other') });
  }
  if (notice.kind === 'expired' || notice.kind === 'withdrawn' || notice.kind === 'closed') {
    // A terminal's press: one an older build ended by its time, one its terminal took back once shown, or one Daoris's close ended
    // (CONFIRMWAIT1a-i, the confirm-wait design §6, §8.3).
    const act = notice.route ? t(`person.route.${notice.route}`, { defaultValue: '' }) : '';
    return act ? t(`notifications.done.${notice.kind}`, { act }) : t('notifications.done.other');
  }
  if (notice.kind === 'lost') {
    // CONFIRMWAIT1a-ii (§7.2, §8.3): one carrying a key a close ended, by its act; else the presses a restart could not bring back, one
    // by its own key, since Chinese has no plural `_one` to pick it.
    const act = notice.route ? t(`person.route.${notice.route}`, { defaultValue: '' }) : '';
    if (act) return t('notifications.done.lostKeyed', { act });
    const count = notice.count ?? 1;
    return count > 1 ? t('notifications.done.lost', { count }) : t('notifications.done.lost_one');
  }
  if (notice.kind === 'newer') return t('notifications.done.newer');
  if (notice.kind === 'look') {
    // A look's news by its code (NOTIFY2h): never the driver's English line. A code a later build says reads as something ended.
    return notice.code ? t(`notifications.done.look.${notice.code}`, { defaultValue: t('notifications.done.other') }) : t('notifications.done.other');
  }
  return t('notifications.done.other');
}

/**
 * *Done and failed* (NOTIFY2f, D196 point 6, the notification design §2.2's fourth group, §2.3, §2.4): what the person need not answer,
 * newest first, each one line with its age, the unread marked; *Open* goes to its session's page where it names one, *Dismiss* removes
 * it, and the group's *Mark all read* reads every one it shows.
 *
 * @remarks
 * **A notice is read once it is shown** (§2.4), since its one line is all it says: in view in the view's area by the same judgement as
 * a press's region, in a window visible and focused (`useShownOnce`), never by being pushed or listed in a window behind. *Open* and
 * *Mark all read* read it too. Read once per machine: the record is the shell's, and every window's slice moves with it.
 *
 * A molecule: the notices come in, and the reads, the doors and the dismissals go out.
 */
export function DoneAndFailed({ notices, onRead, onOpen, onDismiss, now = Date.now() }: {
  /** The window's notices, newest first. */
  notices: Notice[];
  /** The notices shown, *Opened* or named by *Mark all read*, read. */
  onRead: (ids: string[]) => void;
  /** A notice's door: its session's page; absent where it names none. */
  onOpen?: (notice: Notice) => void;
  onDismiss: (notice: Notice) => void;
  /** The clock its ages are read by. */
  now?: number;
}) {
  const { t } = useTranslation();
  const unread = notices.filter((notice) => !notice.read);
  return (
    <div className="grid gap-1.5">
      {unread.length > 0 && (
        <div className="flex justify-end">
          <Button variant="ghost" onClick={() => onRead(unread.map((notice) => notice.id))}>{t('notifications.done.markAll')}</Button>
        </div>
      )}
      <ul className="m-0 grid list-none gap-1.5 p-0">
        {notices.map((notice) => (
          <NoticeRow key={notice.id} notice={notice} onRead={onRead} onOpen={onOpen} onDismiss={onDismiss} now={now} />
        ))}
      </ul>
    </div>
  );
}

/** One notice: its line, its age, whether it is unread, and its acts; read as it is shown. */
function NoticeRow({ notice, onRead, onOpen, onDismiss, now }: {
  notice: Notice;
  onRead: (ids: string[]) => void;
  onOpen?: (notice: Notice) => void;
  onDismiss: (notice: Notice) => void;
  now: number;
}) {
  const { t } = useTranslation();
  const row = useRef<HTMLLIElement>(null);
  const noButtons = useRef<HTMLElement>(null);
  useShownOnce(!notice.read, row, noButtons, () => onRead([notice.id]));
  const failed = notice.kind === 'expired' || notice.kind === 'closed' || notice.kind === 'lost' || notice.kind === 'look'
    || notice.state === 'failed';
  const opens = onOpen && notice.session ? () => {
    if (!notice.read) onRead([notice.id]);
    onOpen(notice);
  } : null;
  return (
    <li ref={row} data-notice={notice.id} className="grid gap-1 rounded-sm border border-line bg-raised px-2.5 py-2">
      <p className={cn('m-0 flex items-baseline gap-1.5 text-body', notice.read ? 'text-ink-soft' : 'text-ink')}>
        {!notice.read && (
          <span role="img" aria-label={t('notifications.done.unread')} className="inline-block h-1.5 w-1.5 shrink-0 rounded-full bg-accent" />
        )}
        <span className={cn('min-w-0 flex-1', failed && !notice.read && 'text-ink-danger')}>{noticeWords(notice, t)}</span>
      </p>
      <div className="flex items-center gap-2">
        <span className="min-w-0 flex-1 text-small text-ink-faint">
          {t('notifications.done.ago', { age: elapsed(notice.at, new Date(now).toISOString()) })}
        </span>
        {opens && <Button variant="ghost" onClick={opens}>{t('notifications.toast.open')}</Button>}
        <Button variant="ghost" onClick={() => onDismiss(notice)}>{t('notifications.done.dismiss')}</Button>
      </div>
    </li>
  );
}
